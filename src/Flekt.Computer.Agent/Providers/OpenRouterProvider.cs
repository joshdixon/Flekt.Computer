using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Flekt.Computer.Agent.Models;
using Microsoft.Extensions.Logging;

namespace Flekt.Computer.Agent.Providers;

public sealed class OpenRouterProvider : ILlmProvider, IAsyncDisposable
{
    private readonly HttpClient _httpClient;
    private readonly string _model;
    private readonly string _apiKey;
    private readonly ILogger<OpenRouterProvider>? _logger;

    public OpenRouterProvider(string model, string apiKey, ILogger<OpenRouterProvider>? logger = null)
    {
        _model = model;
        _apiKey = apiKey;
        _logger = logger;
        
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri("https://openrouter.ai/api/v1/")
        };
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
        _httpClient.DefaultRequestHeaders.Add("HTTP-Referer", "https://flekt.computer");
        _httpClient.DefaultRequestHeaders.Add("X-Title", "Flekt Computer Agent");
    }

    public async IAsyncEnumerable<AgentResult> StreamChatAsync(
        IEnumerable<LlmMessage> messages,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Build messages with conditional fields (OpenRouter doesn't like null tool_calls/tool_call_id)
        var formattedMessages = messages.Select(m =>
        {
            var msg = new Dictionary<string, object?>
            {
                ["role"] = m.Role,
                ["content"] = m.Content.Count == 1 && m.Content[0].Type == "text"
                    ? m.Content[0].Text!
                    : m.Content.Select(c => new Dictionary<string, object?>
                    {
                        ["type"] = c.Type,
                        ["text"] = c.Text,
                        ["image_url"] = c.ImageUrl != null ? new { url = c.ImageUrl.Url } : null
                    }.Where(kv => kv.Value != null).ToDictionary(kv => kv.Key, kv => kv.Value)).ToList()
            };

            // Only add tool_calls for assistant messages that have them
            if (m.ToolCalls != null && m.ToolCalls.Count > 0)
            {
                msg["tool_calls"] = m.ToolCalls.Select(tc => new
                {
                    id = tc.Id,
                    type = "function",
                    function = new
                    {
                        name = tc.Name,
                        arguments = tc.Arguments
                    }
                }).ToList();
            }

            // Only add tool_call_id for tool result messages
            if (!string.IsNullOrEmpty(m.ToolCallId))
            {
                msg["tool_call_id"] = m.ToolCallId;
            }

            // Include reasoning for assistant messages that have it (required for some models)
            if (!string.IsNullOrEmpty(m.Reasoning))
            {
                msg["reasoning"] = m.Reasoning;
            }

            return msg;
        }).ToList();

        var request = new Dictionary<string, object>
        {
            ["model"] = _model,
            ["messages"] = formattedMessages,
            ["stream"] = true,
            ["tools"] = GetToolDefinitions(),
            // Enable reasoning tokens for models that support it
            ["reasoning"] = new Dictionary<string, object>
            {
                ["max_tokens"] = 8000
            }
        };

        // Log the request for debugging
        var requestJson = JsonSerializer.Serialize(request, new JsonSerializerOptions { WriteIndented = false });
        _logger?.LogDebug("Sending request to OpenRouter: {Model}, Messages: {MessageCount}", _model, messages.Count());
        _logger?.LogTrace("OpenRouter request payload: {Payload}", requestJson);

        var response = await _httpClient.PostAsJsonAsync("chat/completions", request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger?.LogError("OpenRouter API error {StatusCode}: {ErrorBody}", (int)response.StatusCode, errorBody);
            _logger?.LogError("Request that caused error: {Request}", requestJson);
            throw new HttpRequestException($"OpenRouter API error {(int)response.StatusCode}: {errorBody}");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);

        var currentToolCall = new Dictionary<int, ToolCallBuilder>();
        var accumulatedContent = new System.Text.StringBuilder();
        var accumulatedReasoning = new System.Text.StringBuilder();

        while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("data: ")) continue;
            if (line == "data: [DONE]") break;

            var json = line[6..]; // Remove "data: " prefix

            OpenRouterStreamChunk? chunk = null;
            try
            {
                chunk = JsonSerializer.Deserialize<OpenRouterStreamChunk>(json);
            }
            catch (JsonException ex)
            {
                _logger?.LogWarning(ex, "Failed to parse OpenRouter response chunk: {Json}", json);
                continue;
            }

            if (chunk?.Choices == null || chunk.Choices.Count == 0)
                continue;

            var delta = chunk.Choices[0].Delta;

            // Handle tool calls (streaming chunks)
            if (delta?.ToolCalls != null)
            {
                foreach (var toolCallChunk in delta.ToolCalls)
                {
                    var index = toolCallChunk.Index;

                    if (!currentToolCall.ContainsKey(index))
                    {
                        currentToolCall[index] = new ToolCallBuilder
                        {
                            Id = toolCallChunk.Id ?? $"call_{Guid.NewGuid():N}",
                            Name = toolCallChunk.Function?.Name ?? ""
                        };
                    }

                    if (toolCallChunk.Function?.Arguments != null)
                    {
                        currentToolCall[index].Arguments += toolCallChunk.Function.Arguments;
                    }
                }
            }

            // Accumulate reasoning content (may come as "reasoning" or "reasoning_content")
            if (!string.IsNullOrEmpty(delta?.Reasoning))
            {
                accumulatedReasoning.Append(delta.Reasoning);
            }
            if (!string.IsNullOrEmpty(delta?.ReasoningContent))
            {
                accumulatedReasoning.Append(delta.ReasoningContent);
            }

            // Accumulate text content (don't yield yet - wait for stream to complete)
            if (!string.IsNullOrEmpty(delta?.Content))
            {
                accumulatedContent.Append(delta.Content);
            }

            // Check if this is the last chunk
            if (chunk.Choices[0].FinishReason != null)
            {
                _logger?.LogInformation("OpenRouter: Stream finished with reason={FinishReason}, ToolCalls={ToolCallCount}, ContentLength={ContentLength}, ReasoningLength={ReasoningLength}",
                    chunk.Choices[0].FinishReason, currentToolCall.Count, accumulatedContent.Length, accumulatedReasoning.Length);

                // Yield reasoning first if present
                if (accumulatedReasoning.Length > 0)
                {
                    _logger?.LogInformation("OpenRouter: Yielding reasoning ({Length} chars)", accumulatedReasoning.Length);
                    yield return new AgentResult
                    {
                        Type = AgentResultType.Reasoning,
                        Content = accumulatedReasoning.ToString()
                    };
                }

                // Yield all accumulated tool calls
                foreach (var tc in currentToolCall.Values)
                {
                    _logger?.LogInformation("OpenRouter: Yielding tool call {Name} (id={Id})", tc.Name, tc.Id);
                    yield return new AgentResult
                    {
                        Type = AgentResultType.ToolCall,
                        ToolCall = new ToolCall
                        {
                            Id = tc.Id,
                            Name = tc.Name,
                            Arguments = tc.Arguments
                        }
                    };
                }

                // Yield accumulated text content as final message
                // ContinueLoop should be true if there were tool calls (agent should continue)
                var hasToolCalls = currentToolCall.Count > 0;
                if (accumulatedContent.Length > 0 || !hasToolCalls)
                {
                    _logger?.LogInformation("OpenRouter: Yielding message (ContinueLoop={ContinueLoop})", hasToolCalls);
                    yield return new AgentResult
                    {
                        Type = AgentResultType.Message,
                        Content = accumulatedContent.ToString(),
                        ContinueLoop = hasToolCalls
                    };
                }

                currentToolCall.Clear();
            }
        }
    }

    public LlmCapabilities Capabilities => new()
    {
        SupportsVision = true,
        SupportsTools = true,
        SupportsStreaming = true,
        MaxImageSize = 20_971_520, // 20MB
        ContextWindow = 200_000
    };

    private static List<object> GetToolDefinitions()
    {
        return new List<object>
        {
            new
            {
                type = "function",
                function = new
                {
                    name = "mouse_move",
                    description = "Move the mouse cursor to the specified pixel coordinates. Coordinates are absolute screen positions where (0,0) is the top-left corner. Always determine coordinates by carefully examining the screenshot first.",
                    parameters = new
                    {
                        type = "object",
                        properties = new
                        {
                            x = new { type = "integer", description = "X coordinate in pixels from the left edge of the screen" },
                            y = new { type = "integer", description = "Y coordinate in pixels from the top edge of the screen" }
                        },
                        required = new[] { "x", "y" }
                    }
                }
            },
            new
            {
                type = "function",
                function = new
                {
                    name = "mouse_click",
                    description = "Click the mouse at the specified pixel coordinates. Always click in the CENTER of UI elements, not on their edges. Coordinates are absolute screen positions where (0,0) is the top-left corner. If coordinates are not provided, clicks at the current cursor position.",
                    parameters = new
                    {
                        type = "object",
                        properties = new
                        {
                            button = new { type = "string", @enum = new[] { "left", "right", "middle" }, description = "Mouse button to click (default: left)" },
                            x = new { type = "integer", description = "X coordinate in pixels from the left edge - should be the horizontal CENTER of the target element" },
                            y = new { type = "integer", description = "Y coordinate in pixels from the top edge - should be the vertical CENTER of the target element" }
                        }
                    }
                }
            },
            new
            {
                type = "function",
                function = new
                {
                    name = "keyboard_type",
                    description = "Type text using the keyboard. Use this for entering text into text fields, search boxes, or any text input. Make sure the target text field is focused (clicked) before typing.",
                    parameters = new
                    {
                        type = "object",
                        properties = new
                        {
                            text = new { type = "string", description = "Text to type" }
                        },
                        required = new[] { "text" }
                    }
                }
            },
            new
            {
                type = "function",
                function = new
                {
                    name = "keyboard_press",
                    description = "Press a key or key combination. Use this for special keys like Enter, Tab, Escape, arrow keys, or keyboard shortcuts with modifiers.",
                    parameters = new
                    {
                        type = "object",
                        properties = new
                        {
                            key = new { type = "string", description = "Key to press (e.g., 'Enter', 'Tab', 'Escape', 'Backspace', 'Delete', 'Up', 'Down', 'Left', 'Right', 'Home', 'End', 'PageUp', 'PageDown', 'F1'-'F12', or any letter/number)" },
                            modifiers = new { type = "array", items = new { type = "string" }, description = "Modifier keys to hold while pressing: 'Ctrl', 'Alt', 'Shift', 'Win'. Example: ['Ctrl', 'Shift'] for Ctrl+Shift+key" }
                        },
                        required = new[] { "key" }
                    }
                }
            },
            new
            {
                type = "function",
                function = new
                {
                    name = "screenshot",
                    description = "Take a screenshot of the current screen. Use this to see the current state of the screen, especially after performing actions to verify the result.",
                    parameters = new
                    {
                        type = "object",
                        properties = new { }
                    }
                }
            }
        };
    }

    public async ValueTask DisposeAsync()
    {
        _httpClient?.Dispose();
        await Task.CompletedTask;
    }

    private class ToolCallBuilder
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Arguments { get; set; } = "";
    }
}

// OpenRouter API response models
internal class OpenRouterStreamChunk
{
    [JsonPropertyName("choices")]
    public List<OpenRouterChoice>? Choices { get; set; }
    
    [JsonPropertyName("usage")]
    public OpenRouterUsage? Usage { get; set; }
}

internal class OpenRouterChoice
{
    [JsonPropertyName("delta")]
    public OpenRouterDelta? Delta { get; set; }
    
    [JsonPropertyName("finish_reason")]
    public string? FinishReason { get; set; }
}

internal class OpenRouterDelta
{
    [JsonPropertyName("content")]
    public string? Content { get; set; }

    [JsonPropertyName("reasoning")]
    public string? Reasoning { get; set; }

    [JsonPropertyName("reasoning_content")]
    public string? ReasoningContent { get; set; }

    [JsonPropertyName("tool_calls")]
    public List<OpenRouterToolCall>? ToolCalls { get; set; }
}

internal class OpenRouterReasoningDetails
{
    [JsonPropertyName("redacted_reasoning_content")]
    public string? RedactedContent { get; set; }

    [JsonPropertyName("encrypted_content")]
    public string? EncryptedContent { get; set; }
}

internal class OpenRouterToolCall
{
    [JsonPropertyName("index")]
    public int Index { get; set; }
    
    [JsonPropertyName("id")]
    public string? Id { get; set; }
    
    [JsonPropertyName("type")]
    public string? Type { get; set; }
    
    [JsonPropertyName("function")]
    public OpenRouterFunction? Function { get; set; }
}

internal class OpenRouterFunction
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }
    
    [JsonPropertyName("arguments")]
    public string? Arguments { get; set; }
}

internal class OpenRouterUsage
{
    [JsonPropertyName("prompt_tokens")]
    public int PromptTokens { get; set; }
    
    [JsonPropertyName("completion_tokens")]
    public int CompletionTokens { get; set; }
    
    [JsonPropertyName("total_tokens")]
    public int TotalTokens { get; set; }
}


