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
        var request = new
        {
            model = _model,
            messages = messages.Select(m => new
            {
                role = m.Role,
                content = m.Content.Count == 1 && m.Content[0].Type == "text"
                    ? (object)m.Content[0].Text!
                    : m.Content.Select(c => new
                    {
                        type = c.Type,
                        text = c.Text,
                        image_url = c.ImageUrl != null ? new { url = c.ImageUrl.Url } : null
                    }).ToList(),
                tool_calls = m.ToolCalls?.Select(tc => new
                {
                    id = tc.Id,
                    type = "function",
                    function = new
                    {
                        name = tc.Name,
                        arguments = tc.Arguments
                    }
                }).ToList(),
                tool_call_id = m.ToolCallId
            }),
            stream = true,
            tools = GetToolDefinitions()
        };

        _logger?.LogDebug("Sending request to OpenRouter: {Model}", _model);

        var response = await _httpClient.PostAsJsonAsync("chat/completions", request, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);

        var currentToolCall = new Dictionary<int, ToolCallBuilder>();

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

            // Handle text content
            if (!string.IsNullOrEmpty(delta?.Content))
            {
                yield return new AgentResult
                {
                    Type = AgentResultType.Message,
                    Content = delta.Content,
                    ContinueLoop = false
                };
            }

            // Check if this is the last chunk
            if (chunk.Choices[0].FinishReason != null)
            {
                // Yield all accumulated tool calls
                foreach (var tc in currentToolCall.Values)
                {
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
                    description = "Move the mouse cursor to specified coordinates",
                    parameters = new
                    {
                        type = "object",
                        properties = new
                        {
                            x = new { type = "integer", description = "X coordinate" },
                            y = new { type = "integer", description = "Y coordinate" }
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
                    description = "Click the mouse at current position or specified coordinates",
                    parameters = new
                    {
                        type = "object",
                        properties = new
                        {
                            button = new { type = "string", @enum = new[] { "left", "right", "middle" }, description = "Mouse button to click (default: left)" },
                            x = new { type = "integer", description = "Optional X coordinate" },
                            y = new { type = "integer", description = "Optional Y coordinate" }
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
                    description = "Type text using the keyboard",
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
                    description = "Press a key or key combination",
                    parameters = new
                    {
                        type = "object",
                        properties = new
                        {
                            key = new { type = "string", description = "Key to press (e.g., 'Enter', 'Tab', 'Escape')" },
                            modifiers = new { type = "array", items = new { type = "string" }, description = "Modifier keys (Ctrl, Alt, Shift)" }
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
                    description = "Take a screenshot of the current screen",
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
    
    [JsonPropertyName("tool_calls")]
    public List<OpenRouterToolCall>? ToolCalls { get; set; }
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

