using System.Net;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

using Flekt.Computer.Agent.Models;

using Microsoft.Extensions.Logging;

namespace Flekt.Computer.Agent.Providers;

public sealed class OpenRouterProvider : ILlmProvider, IAsyncDisposable
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OpenRouterProvider>? _logger;
    private readonly string _model;

    public OpenRouterProvider(string model, string apiKey, ILogger<OpenRouterProvider>? logger = null)
    {
        _model = model;
        _logger = logger;

        // Configure proxy for debugging with HTTP Toolkit
        var handler = new HttpClientHandler
        {
            Proxy = new WebProxy("http://localhost:8000"),
            UseProxy = true,
            // Ignore SSL errors for proxy debugging
            ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
        };

        _httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://openrouter.ai/api/v1/"),
            // Increase timeout for large vision requests with multiple screenshots
            Timeout = TimeSpan.FromMinutes(5)
        };
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
        _httpClient.DefaultRequestHeaders.Add("HTTP-Referer", "https://flekt.computer");
        _httpClient.DefaultRequestHeaders.Add("X-Title", "Flekt Computer Agent");
    }

    public async ValueTask DisposeAsync()
    {
        _httpClient?.Dispose();
        await Task.CompletedTask;
    }

    public async IAsyncEnumerable<AgentResult> StreamChatAsync(
        IEnumerable<LlmMessage> messages,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Build messages with conditional fields (OpenRouter doesn't like null tool_calls/tool_call_id)
        List<Dictionary<string, object?>> formattedMessages = messages.Select(m =>
            {
                // Handle content - use empty string if no content parts (for assistant messages with only tool calls)
                object? content;
                if (m.Content.Count == 0)
                {
                    content = ""; // Empty string, not empty array - Gemini doesn't like empty arrays
                }
                else if (m.Content.Count == 1 && m.Content[0].Type == "text")
                {
                    content = m.Content[0].Text!;
                }
                else
                {
                    content = m.Content.Select(c => new Dictionary<string, object?>
                            {
                                ["type"] = c.Type,
                                ["text"] = c.Text,
                                ["image_url"] = c.ImageUrl != null ? new { url = c.ImageUrl.Url } : null
                            }.Where(kv => kv.Value != null)
                            .ToDictionary(kv => kv.Key, kv => kv.Value))
                        .ToList();
                }

                var msg = new Dictionary<string, object?>
                {
                    ["role"] = m.Role,
                    ["content"] = content
                };

                // Only add tool_calls for assistant messages that have them
                if (m.ToolCalls != null && m.ToolCalls.Count > 0)
                {
                    msg["tool_calls"] = m.ToolCalls.Select(tc => new Dictionary<string, object?>
                        {
                            ["id"] = tc.Id,
                            ["type"] = "function",
                            ["function"] = new
                            {
                                name = tc.Name,
                                arguments = tc.Arguments
                            }
                        })
                        .ToList();
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

                // Include reasoning_details for Gemini models (must be preserved unchanged)
                if (m.ReasoningDetails.HasValue && m.ReasoningDetails.Value.ValueKind != JsonValueKind.Null)
                {
                    JsonElement details = m.ReasoningDetails.Value;
                    if (!(details.ValueKind == JsonValueKind.Array && details.GetArrayLength() == 0))
                    {
                        msg["reasoning_details"] = details;
                    }
                }

                return msg;
            })
            .ToList();

        var request = new Dictionary<string, object>
        {
            ["model"] = _model,
            ["messages"] = formattedMessages,
            ["stream"] = true,
            ["tools"] = GetToolDefinitions(),
            // Enable reasoning with low effort (reduces rambling for Gemini 3)
            ["reasoning"] = new Dictionary<string, object>
            {
                ["enabled"] = true,
                ["effort"] = "low"
            },
            // Force consistent provider to avoid context issues between Google AI Studio vs Google
            ["provider"] = new Dictionary<string, object>
            {
                ["order"] = new[] { "Google AI Studio" },
                ["allow_fallbacks"] = false
            }
        };

        // Log the request for debugging
        string requestJson = JsonSerializer.Serialize(request, new JsonSerializerOptions { WriteIndented = false });
        _logger?.LogDebug("Sending request to OpenRouter: {Model}, Messages: {MessageCount}", _model, messages.Count());
        _logger?.LogTrace("OpenRouter request payload: {Payload}", requestJson);

        HttpResponseMessage response = await _httpClient.PostAsJsonAsync("chat/completions", request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            string errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger?.LogError("OpenRouter API error {StatusCode}: {ErrorBody}", (int)response.StatusCode, errorBody);
            _logger?.LogError("Request that caused error: {Request}", requestJson);

            throw new HttpRequestException($"OpenRouter API error {(int)response.StatusCode}: {errorBody}");
        }

        await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);

        var currentToolCall = new Dictionary<int, ToolCallBuilder>();
        var accumulatedContent = new StringBuilder();
        var accumulatedReasoning = new StringBuilder();
        JsonElement? capturedReasoningDetails = null;
        bool hasYieldedFinalMessage = false; // Track if we've already processed a finish_reason
        bool everHadToolCalls = false; // Track if ANY tool calls were seen across entire stream
        bool hasEncryptedReasoning = false; // Track if encrypted reasoning (thoughtSignature) was seen

        while (!cancellationToken.IsCancellationRequested)
        {
            string? line = await reader.ReadLineAsync(cancellationToken);
            if (line == null)
                break;

            // Skip empty lines and SSE comments
            if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("data: "))
                continue;

            string data = line[6..];
            if (data == "[DONE]")
                break;

            if (!TryParseChunk(data, out OpenRouterStreamChunk? chunk) || chunk?.Choices is not { Count: > 0 })
                continue;

            OpenRouterChoice choice = chunk!.Choices![0];
            OpenRouterDelta? delta = choice.Delta;

            // Accumulate tool calls
            if (delta?.ToolCalls != null)
            {
                everHadToolCalls = true;
                foreach (OpenRouterToolCall tc in delta.ToolCalls)
                {
                    if (!currentToolCall.TryGetValue(tc.Index, out ToolCallBuilder? builder))
                        currentToolCall[tc.Index] = builder = new ToolCallBuilder { Id = tc.Id ?? "", Name = tc.Function?.Name ?? "" };

                    if (tc.Function?.Arguments != null)
                        builder.Arguments += tc.Function.Arguments;
                }
            }

            // Accumulate reasoning (supports both field names)
            if (!string.IsNullOrEmpty(delta?.Reasoning))
                accumulatedReasoning.Append(delta.Reasoning);
            if (!string.IsNullOrEmpty(delta?.ReasoningContent))
                accumulatedReasoning.Append(delta.ReasoningContent);

            // Accumulate content
            if (!string.IsNullOrEmpty(delta?.Content))
                accumulatedContent.Append(delta.Content);

            // Capture reasoning_details from choice or delta (for Gemini models)
            CaptureReasoningDetails(choice.ReasoningDetails, currentToolCall, ref capturedReasoningDetails, ref hasEncryptedReasoning);
            CaptureReasoningDetails(delta?.ReasoningDetails, currentToolCall, ref capturedReasoningDetails, ref hasEncryptedReasoning);

            // Handle finish - only process first finish_reason (Gemini 3 may send multiple)
            if (choice.FinishReason == null || hasYieldedFinalMessage)
                continue;

            hasYieldedFinalMessage = true;

            // Yield reasoning first if present
            if (accumulatedReasoning.Length > 0)
            {
                yield return new AgentResult
                {
                    Type = AgentResultType.Reasoning,
                    Content = accumulatedReasoning.ToString()
                };
            }

            // Yield all accumulated tool calls
            foreach (ToolCallBuilder tc in currentToolCall.Values)
            {
                yield return new AgentResult
                {
                    Type = AgentResultType.ToolCall,
                    ToolCall = new ToolCall
                    {
                        Id = tc.Id,
                        Name = tc.Name,
                        Arguments = tc.Arguments,
                        ThoughtSignature = tc.ThoughtSignature
                    }
                };
            }

            // Continue if there were tool calls (agent needs to execute them and continue)
            bool shouldContinue = everHadToolCalls;

            yield return new AgentResult
            {
                Type = AgentResultType.Message,
                Content = accumulatedContent.ToString(),
                ContinueLoop = shouldContinue,
                ReasoningDetails = capturedReasoningDetails
            };

            currentToolCall.Clear();
        }
    }

    private bool TryParseChunk(string json, out OpenRouterStreamChunk? chunk)
    {
        try
        {
            chunk = JsonSerializer.Deserialize<OpenRouterStreamChunk>(json);
            return true;
        }
        catch (JsonException ex)
        {
            _logger?.LogWarning(ex, "Failed to parse chunk: {Json}", json);
            chunk = null;
            return false;
        }
    }

    private void CaptureReasoningDetails(
        JsonElement? details,
        Dictionary<int, ToolCallBuilder> toolCalls,
        ref JsonElement? captured,
        ref bool hasEncrypted)
    {
        if (details is not { ValueKind: not JsonValueKind.Null } d)
            return;
        if (d.ValueKind == JsonValueKind.Array && d.GetArrayLength() == 0)
            return;

        captured = d;
        ExtractThoughtSignaturesFromReasoningDetails(d, toolCalls, ref hasEncrypted, _logger);
    }

    public LlmCapabilities Capabilities => new()
    {
        SupportsVision = true,
        SupportsTools = true,
        SupportsStreaming = true,
        MaxImageSize = 20_971_520, // 20MB
        ContextWindow = 200_000
    };

    private static List<object> GetToolDefinitions() => new()
    {
        new
        {
            type = "function",
            function = new
            {
                name = "mouse_move",
                description
                    = "Move the mouse cursor to the specified pixel coordinates. Coordinates are absolute screen positions where (0,0) is the top-left corner. Always determine coordinates by carefully examining the screenshot first.",
                parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        x = new
                        {
                            type = "integer",
                            description = "X coordinate in pixels from the left edge of the screen"
                        },
                        y = new
                        {
                            type = "integer",
                            description = "Y coordinate in pixels from the top edge of the screen"
                        }
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
                description
                    = "Click the mouse at the specified pixel coordinates. Always click in the CENTER of UI elements, not on their edges. Coordinates are absolute screen positions where (0,0) is the top-left corner. If coordinates are not provided, clicks at the current cursor position.",
                parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        button = new
                        {
                            type = "string",
                            @enum = new[] { "left", "right", "middle" },
                            description = "Mouse button to click (default: left)"
                        },
                        x = new
                        {
                            type = "integer",
                            description = "X coordinate in pixels from the left edge - should be the horizontal CENTER of the target element"
                        },
                        y = new
                        {
                            type = "integer",
                            description = "Y coordinate in pixels from the top edge - should be the vertical CENTER of the target element"
                        }
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
                description
                    = "Type text using the keyboard. Use this for entering text into text fields, search boxes, or any text input. Make sure the target text field is focused (clicked) before typing.",
                parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        text = new
                        {
                            type = "string",
                            description = "Text to type"
                        }
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
                description
                    = "Press a key or key combination. Use this for special keys like Enter, Tab, Escape, arrow keys, or keyboard shortcuts with modifiers.",
                parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        key = new
                        {
                            type = "string",
                            description
                                = "Key to press (e.g., 'Enter', 'Tab', 'Escape', 'Backspace', 'Delete', 'Up', 'Down', 'Left', 'Right', 'Home', 'End', 'PageUp', 'PageDown', 'F1'-'F12', or any letter/number)"
                        },
                        modifiers = new
                        {
                            type = "array",
                            items = new { type = "string" },
                            description
                                = "Modifier keys to hold while pressing: 'Ctrl', 'Alt', 'Shift', 'Win'. Example: ['Ctrl', 'Shift'] for Ctrl+Shift+key"
                        }
                    },
                    required = new[] { "key" }
                }
            }
        }
        // new
        // {
        //     type = "function",
        //     function = new
        //     {
        //         name = "screenshot",
        //         description
        //             = "Take a screenshot of the current screen. Use this to see the current state of the screen, especially after performing actions to verify the result.",
        //         parameters = new
        //         {
        //             type = "object",
        //             properties = new { }
        //         }
        //     }
        // }
    };

    /// <summary>
    ///     Extracts encrypted reasoning data from reasoning_details and attaches to matching tool calls.
    ///     Gemini returns reasoning_details like:
    ///     [{"id":"tool_call_id","type":"reasoning.encrypted","data":"...","format":"google-gemini-v1"}]
    ///     The "data" field should be used as the thought_signature for the tool call with matching "id".
    /// </summary>
    private static void ExtractThoughtSignaturesFromReasoningDetails(
        JsonElement details,
        Dictionary<int, ToolCallBuilder> currentToolCall,
        ref bool hasEncryptedReasoning,
        ILogger? logger)
    {
        if (details.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (JsonElement item in details.EnumerateArray())
        {
            // Check if this is an encrypted reasoning entry
            if (!item.TryGetProperty("type", out JsonElement typeElement) || typeElement.GetString() != "reasoning.encrypted")
            {
                continue;
            }

            // Get the tool call ID and data
            if (!item.TryGetProperty("id", out JsonElement idElement) || !item.TryGetProperty("data", out JsonElement dataElement))
            {
                continue;
            }

            string? toolCallId = idElement.GetString();
            string? encryptedData = dataElement.GetString();

            if (string.IsNullOrEmpty(toolCallId) || string.IsNullOrEmpty(encryptedData))
            {
                continue;
            }

            hasEncryptedReasoning = true;

            // Find the matching tool call and attach the thought signature
            foreach (ToolCallBuilder tc in currentToolCall.Values)
            {
                if (tc.Id == toolCallId)
                {
                    tc.ThoughtSignature = encryptedData;
                    logger?.LogInformation("OpenRouter: Extracted thought_signature from reasoning_details for tool call {Id}", toolCallId);

                    break;
                }
            }
        }
    }

    private class ToolCallBuilder
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Arguments { get; set; } = "";
        public string? ThoughtSignature { get; set; }
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

    /// <summary>
    ///     Reasoning details for Gemini models - must be preserved and sent back.
    /// </summary>
    [JsonPropertyName("reasoning_details")]
    public JsonElement? ReasoningDetails { get; set; }
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

    /// <summary>
    ///     Reasoning details for Gemini models - must be preserved and sent back.
    /// </summary>
    [JsonPropertyName("reasoning_details")]
    public JsonElement? ReasoningDetails { get; set; }
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
