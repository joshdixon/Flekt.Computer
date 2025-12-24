using System.Runtime.CompilerServices;
using System.Text.Json;

using Flekt.Computer.Agent.Models;

using Google.GenAI;
using Google.GenAI.Types;

using Microsoft.Extensions.Logging;

namespace Flekt.Computer.Agent.Providers;

public sealed class GeminiProvider : ILlmProvider
{
    private readonly Client _client;
    private readonly ILogger<GeminiProvider>? _logger;
    private readonly string _model;

    public GeminiProvider(string model, string apiKey, ILogger<GeminiProvider>? logger = null)
    {
        _model = model;
        _logger = logger;
        _client = new Client(apiKey: apiKey);
    }

    public LlmCapabilities Capabilities => new()
    {
        SupportsVision = true,
        SupportsTools = true,
        SupportsStreaming = true,
        MaxImageSize = 20_971_520, // 20MB
        ContextWindow = 1_000_000  // Gemini has 1M context
    };

    public async IAsyncEnumerable<AgentResult> StreamChatAsync(
        IEnumerable<LlmMessage> messages,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        List<Content> contents = ConvertMessages(messages);
        GenerateContentConfig config = CreateConfig();

        _logger?.LogDebug("GeminiProvider: Sending {Count} messages to {Model}", contents.Count, _model);

        var pendingFunctionCalls = new List<FunctionCall>();
        var accumulatedText = new System.Text.StringBuilder();
        bool hasYieldedFinal = false;

        await foreach (GenerateContentResponse chunk in _client.Models.GenerateContentStreamAsync(
            model: _model,
            contents: contents,
            config: config))
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            if (chunk.Candidates == null || chunk.Candidates.Count == 0)
                continue;

            Candidate candidate = chunk.Candidates[0];
            if (candidate.Content?.Parts == null)
                continue;

            foreach (Part part in candidate.Content.Parts)
            {
                // Handle thinking/reasoning
                if (part.Thought == true && !string.IsNullOrEmpty(part.Text))
                {
                    yield return new AgentResult
                    {
                        Type = AgentResultType.Reasoning,
                        Content = part.Text
                    };
                }
                // Handle function calls
                else if (part.FunctionCall != null)
                {
                    pendingFunctionCalls.Add(part.FunctionCall);
                    _logger?.LogDebug("GeminiProvider: Received function call {Name}", part.FunctionCall.Name);
                }
                // Handle text content
                else if (!string.IsNullOrEmpty(part.Text))
                {
                    accumulatedText.Append(part.Text);
                }
            }

            // Check for finish
            if (candidate.FinishReason != null && !hasYieldedFinal)
            {
                hasYieldedFinal = true;
                _logger?.LogInformation("GeminiProvider: Finished with reason {Reason}, FunctionCalls={Count}",
                    candidate.FinishReason, pendingFunctionCalls.Count);

                // Yield function calls
                foreach (FunctionCall fc in pendingFunctionCalls)
                {
                    string args = fc.Args != null
                        ? JsonSerializer.Serialize(fc.Args)
                        : "{}";

                    yield return new AgentResult
                    {
                        Type = AgentResultType.ToolCall,
                        ToolCall = new ToolCall
                        {
                            Id = fc.Id ?? Guid.NewGuid().ToString(),
                            Name = fc.Name ?? "unknown",
                            Arguments = args
                        }
                    };
                }

                // Yield final message
                yield return new AgentResult
                {
                    Type = AgentResultType.Message,
                    Content = accumulatedText.ToString(),
                    ContinueLoop = pendingFunctionCalls.Count > 0
                };
            }
        }

        // If we never got a finish reason, yield what we have
        if (!hasYieldedFinal)
        {
            foreach (FunctionCall fc in pendingFunctionCalls)
            {
                string args = fc.Args != null
                    ? JsonSerializer.Serialize(fc.Args)
                    : "{}";

                yield return new AgentResult
                {
                    Type = AgentResultType.ToolCall,
                    ToolCall = new ToolCall
                    {
                        Id = fc.Id ?? Guid.NewGuid().ToString(),
                        Name = fc.Name ?? "unknown",
                        Arguments = args
                    }
                };
            }

            yield return new AgentResult
            {
                Type = AgentResultType.Message,
                Content = accumulatedText.ToString(),
                ContinueLoop = pendingFunctionCalls.Count > 0
            };
        }
    }

    private List<Content> ConvertMessages(IEnumerable<LlmMessage> messages)
    {
        var contents = new List<Content>();

        foreach (LlmMessage msg in messages)
        {
            // Map roles: system -> user (Gemini doesn't have system role in contents)
            // user -> user, assistant -> model, tool -> user (with function response)
            string role = msg.Role switch
            {
                "system" => "user",
                "assistant" => "model",
                "tool" => "user",
                _ => "user"
            };

            var parts = new List<Part>();

            // Handle tool result messages
            if (msg.Role == "tool" && !string.IsNullOrEmpty(msg.ToolCallId))
            {
                // Find the function name from the content or use a default
                string? functionName = null;

                // Try to parse content as JSON to get function info
                if (msg.Content.Count > 0 && msg.Content[0].Type == "text")
                {
                    try
                    {
                        var resultJson = JsonSerializer.Deserialize<Dictionary<string, object>>(msg.Content[0].Text ?? "{}");
                        functionName = msg.ToolCallId; // Use tool call ID as function name reference
                    }
                    catch { }
                }

                parts.Add(new Part
                {
                    FunctionResponse = new FunctionResponse
                    {
                        Id = msg.ToolCallId,
                        Name = functionName ?? "unknown",
                        Response = new Dictionary<string, object>
                        {
                            ["result"] = msg.Content.FirstOrDefault()?.Text ?? ""
                        }
                    }
                });
            }
            // Handle assistant messages with tool calls
            else if (msg.Role == "assistant" && msg.ToolCalls != null && msg.ToolCalls.Count > 0)
            {
                foreach (ToolCall tc in msg.ToolCalls)
                {
                    var args = JsonSerializer.Deserialize<Dictionary<string, object>>(tc.Arguments);
                    parts.Add(new Part
                    {
                        FunctionCall = new FunctionCall
                        {
                            Id = tc.Id,
                            Name = tc.Name,
                            Args = args
                        }
                    });
                }
            }
            // Handle regular content
            else
            {
                foreach (LlmContent content in msg.Content)
                {
                    if (content.Type == "text" && !string.IsNullOrEmpty(content.Text))
                    {
                        parts.Add(new Part { Text = content.Text });
                    }
                    else if (content.Type == "image_url" && content.ImageUrl != null)
                    {
                        // Parse data URL: data:image/png;base64,XXXXX
                        string url = content.ImageUrl.Url;
                        if (url.StartsWith("data:"))
                        {
                            int commaIndex = url.IndexOf(',');
                            if (commaIndex > 0)
                            {
                                string header = url[5..commaIndex]; // Remove "data:"
                                string base64Data = url[(commaIndex + 1)..];

                                // Parse mime type from header (e.g., "image/png;base64")
                                string mimeType = header.Split(';')[0];

                                parts.Add(new Part
                                {
                                    InlineData = new Blob
                                    {
                                        MimeType = mimeType,
                                        Data = Convert.FromBase64String(base64Data)
                                    }
                                });
                            }
                        }
                    }
                }
            }

            if (parts.Count > 0)
            {
                contents.Add(new Content
                {
                    Role = role,
                    Parts = parts
                });
            }
        }

        return contents;
    }

    private GenerateContentConfig CreateConfig()
    {
        return new GenerateContentConfig
        {
            Tools = new List<Tool>
            {
                new Tool
                {
                    FunctionDeclarations = GetFunctionDeclarations()
                }
            }
        };
    }

    private static List<FunctionDeclaration> GetFunctionDeclarations() => new()
    {
        new FunctionDeclaration
        {
            Name = "mouse_move",
            Description = "Move the mouse cursor to the specified pixel coordinates.",
            Parameters = new Schema
            {
                Type = Google.GenAI.Types.Type.OBJECT,
                Properties = new Dictionary<string, Schema>
                {
                    ["x"] = new Schema { Type = Google.GenAI.Types.Type.INTEGER, Description = "X coordinate in pixels" },
                    ["y"] = new Schema { Type = Google.GenAI.Types.Type.INTEGER, Description = "Y coordinate in pixels" }
                },
                Required = new List<string> { "x", "y" }
            }
        },
        new FunctionDeclaration
        {
            Name = "mouse_click",
            Description = "Click the mouse at the specified pixel coordinates. Click in the CENTER of UI elements.",
            Parameters = new Schema
            {
                Type = Google.GenAI.Types.Type.OBJECT,
                Properties = new Dictionary<string, Schema>
                {
                    ["button"] = new Schema
                    {
                        Type = Google.GenAI.Types.Type.STRING,
                        Description = "Mouse button: left, right, or middle",
                        Enum = new List<string> { "left", "right", "middle" }
                    },
                    ["x"] = new Schema { Type = Google.GenAI.Types.Type.INTEGER, Description = "X coordinate in pixels" },
                    ["y"] = new Schema { Type = Google.GenAI.Types.Type.INTEGER, Description = "Y coordinate in pixels" }
                }
            }
        },
        new FunctionDeclaration
        {
            Name = "keyboard_type",
            Description = "Type text using the keyboard.",
            Parameters = new Schema
            {
                Type = Google.GenAI.Types.Type.OBJECT,
                Properties = new Dictionary<string, Schema>
                {
                    ["text"] = new Schema { Type = Google.GenAI.Types.Type.STRING, Description = "Text to type" }
                },
                Required = new List<string> { "text" }
            }
        },
        new FunctionDeclaration
        {
            Name = "keyboard_press",
            Description = "Press a key or key combination.",
            Parameters = new Schema
            {
                Type = Google.GenAI.Types.Type.OBJECT,
                Properties = new Dictionary<string, Schema>
                {
                    ["key"] = new Schema
                    {
                        Type = Google.GenAI.Types.Type.STRING,
                        Description = "Key to press (Enter, Tab, Escape, etc.)"
                    },
                    ["modifiers"] = new Schema
                    {
                        Type = Google.GenAI.Types.Type.ARRAY,
                        Items = new Schema { Type = Google.GenAI.Types.Type.STRING },
                        Description = "Modifier keys: Ctrl, Alt, Shift, Win"
                    }
                },
                Required = new List<string> { "key" }
            }
        }
    };
}
