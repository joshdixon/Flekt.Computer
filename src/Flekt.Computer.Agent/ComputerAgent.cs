using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

using Flekt.Computer.Abstractions;
using Flekt.Computer.Agent.Models;
using Flekt.Computer.Agent.Providers;
using Flekt.Computer.Agent.Services;

using Microsoft.Extensions.Logging;

namespace Flekt.Computer.Agent;

/// <summary>
///     AI agent that can control computers using vision-language models.
///     Inspired by CUA's agent SDK with async foreach streaming pattern.
/// </summary>
public sealed class ComputerAgent : IAsyncDisposable
{
    // JSON options for case-insensitive deserialization (LLMs may use lowercase property names)
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly IComputer _computer;

    // Persistent conversation history - grows over time, screenshots filtered at query time
    private readonly List<LlmMessage> _llmMessageHistory = new();
    private readonly ILlmProvider _llmProvider;
    private readonly ILogger<ComputerAgent>? _logger;
    private readonly IOmniParser? _omniParser;
    private readonly ComputerAgentOptions _options;
    private ScreenSize _screenSize;

    public ComputerAgent(
        string model,
        IComputer computer,
        string apiKey,
        ComputerAgentOptions? options = null,
        ILoggerFactory? loggerFactory = null)
    {
        _computer = computer;
        _options = options ?? new ComputerAgentOptions();
        _logger = loggerFactory?.CreateLogger<ComputerAgent>();

        // Select provider based on model name
        // Gemini models: use direct Google GenAI SDK
        // Others: use OpenRouter for routing
        if (model.StartsWith("gemini-", StringComparison.OrdinalIgnoreCase) ||
            model.Contains("/gemini-", StringComparison.OrdinalIgnoreCase))
        {
            // Extract just the model name if it has a prefix (e.g., "google/gemini-2.0-flash" -> "gemini-2.0-flash")
            string geminiModel = model.Contains('/') ? model.Split('/').Last() : model;
            _llmProvider = new GeminiProvider(geminiModel, apiKey, loggerFactory?.CreateLogger<GeminiProvider>());
            _logger?.LogInformation("ComputerAgent: Using GeminiProvider for model {Model}", geminiModel);
        }
        else
        {
            _llmProvider = new OpenRouterProvider(model, apiKey, loggerFactory?.CreateLogger<OpenRouterProvider>());
            _logger?.LogInformation("ComputerAgent: Using OpenRouterProvider for model {Model}", model);
        }

        // Initialize OmniParser if enabled
        if (_options.EnableOmniParser)
        {
            if (string.IsNullOrEmpty(_options.OmniParserBaseUrl))
            {
                throw new ArgumentException(
                    "OmniParserBaseUrl is required when EnableOmniParser is true");
            }

            _omniParser = new CloudOmniParser(
                _options.OmniParserBaseUrl,
                loggerFactory?.CreateLogger<CloudOmniParser>());

            _logger?.LogInformation("ComputerAgent: OmniParser enabled");
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_llmProvider is IAsyncDisposable disposable)
        {
            await disposable.DisposeAsync();
        }
    }

    /// <summary>
    ///     Run the agent with streaming results (async foreach pattern like CUA).
    /// </summary>
    /// <param name="messages">Conversation history</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Async enumerable of agent results</returns>
    public async IAsyncEnumerable<AgentResult> RunAsync(
        IEnumerable<AgentMessage> messages,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        List<AgentMessage> initialMessages = messages.ToList();
        _logger?.LogInformation("ComputerAgent: Run started with {Count} messages", initialMessages.Count);

        // Convert initial messages to LlmMessages and add to history (only on first call)
        if (_llmMessageHistory.Count == 0)
        {
            AddSystemPrompt();

            foreach (AgentMessage msg in initialMessages)
            {
                _llmMessageHistory.Add(ConvertToLlmMessage(msg));
            }
        }

        int iterations = 0;

        while (!cancellationToken.IsCancellationRequested && iterations < _options.MaxIterations)
        {
            iterations++;
            _logger?.LogInformation("ComputerAgent: Iteration {Iteration}/{MaxIterations} started", iterations, _options.MaxIterations);

            // Take screenshot and add to history as user message
            _logger?.LogDebug("ComputerAgent: Taking screenshot...");
            ScreenshotContext currentContext;
            try
            {
                currentContext = await CaptureScreenshotAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "ComputerAgent: Failed to take screenshot");

                throw;
            }

            // Yield the screenshot so callers can see/save it
            yield return new AgentResult
            {
                Type = AgentResultType.Screenshot,
                Screenshot = currentContext.ScreenshotBase64,
                AnnotatedScreenshot = currentContext.OmniParserResult?.AnnotatedImageBase64,
                OmniParserElements = currentContext.OmniParserResult?.Elements
                    .Select(e => new OmniParserElementInfo
                    {
                        Id = e.Id,
                        Type = e.Type,
                        Content = e.Content,
                        CenterX = e.Center[0],
                        CenterY = e.Center[1],
                        Interactivity = e.Interactivity
                    })
                    .ToList()
            };

            // Add screenshot as user message to persistent history
            _llmMessageHistory.Add(CreateScreenshotMessage(currentContext));

            // Prepare messages for LLM (system prompt + filtered history)
            List<LlmMessage> contextMessages = FilterScreenshots();
            _logger?.LogInformation("ComputerAgent: Prepared {Count} messages for LLM", contextMessages.Count);

            // Call LLM with streaming
            var pendingToolCalls = new List<ToolCall>();
            string? assistantContent = null;
            bool continueLoop = false;
            JsonElement? reasoningDetails = null;

            _logger?.LogInformation("ComputerAgent: Calling LLM...");
            await foreach (AgentResult chunk in _llmProvider.StreamChatAsync(contextMessages, cancellationToken))
            {
                if (chunk.Type == AgentResultType.Reasoning)
                {
                    yield return chunk;

                    continue;
                }

                if (chunk.Type == AgentResultType.ToolCall && chunk.ToolCall != null)
                {
                    pendingToolCalls.Add(chunk.ToolCall);

                    yield return chunk;
                }

                if (chunk.Type == AgentResultType.Message)
                {
                    assistantContent = chunk.Content;
                    continueLoop = chunk.ContinueLoop;
                    reasoningDetails = chunk.ReasoningDetails;

                    yield return chunk;
                }
            }

            bool hasToolCalls = pendingToolCalls.Count > 0;
            _logger?.LogInformation(
                "ComputerAgent: LLM response received - ToolCalls={ToolCallCount}, HasContent={HasContent}, ContinueLoop={ContinueLoop}",
                pendingToolCalls.Count,
                !string.IsNullOrEmpty(assistantContent),
                continueLoop);

            // Add assistant message to history
            _llmMessageHistory.Add(new LlmMessage
            {
                Role = "assistant",
                Content = string.IsNullOrEmpty(assistantContent)
                    ? new List<LlmContent>()
                    : new List<LlmContent>
                    {
                        new()
                        {
                            Type = "text",
                            Text = assistantContent
                        }
                    },
                ToolCalls = hasToolCalls ? pendingToolCalls : null,
                ReasoningDetails = reasoningDetails
            });

            if (hasToolCalls)
            {
                // Execute each tool call and add results to history
                foreach (ToolCall toolCall in pendingToolCalls)
                {
                    _logger?.LogInformation("ComputerAgent: Executing tool {ToolName} (id={ToolId})", toolCall.Name, toolCall.Id);
                    AgentMessage toolResult = await ExecuteToolCall(toolCall, cancellationToken);

                    // Add tool result to history
                    _llmMessageHistory.Add(new LlmMessage
                    {
                        Role = "tool",
                        Content = new List<LlmContent>
                        {
                            new()
                            {
                                Type = "text",
                                Text = toolResult.Content ?? ""
                            }
                        },
                        ToolCallId = toolCall.Id
                    });

                    _logger?.LogInformation("ComputerAgent: Tool {ToolName} executed, result added to history", toolCall.Name);
                }

                _logger?.LogInformation("ComputerAgent: All {Count} tool calls executed, continuing loop", pendingToolCalls.Count);
            }

            // Check if we should stop
            if (!continueLoop && !hasToolCalls)
            {
                _logger?.LogInformation("ComputerAgent: Stopping - no tool calls and continueLoop=false after {Iterations} iterations", iterations);

                yield break;
            }

            if (hasToolCalls)
            {
                if (_options.ScreenshotDelay > TimeSpan.Zero)
                {
                    _logger?.LogDebug("ComputerAgent: Waiting {Delay}ms for UI to settle", _options.ScreenshotDelay.TotalMilliseconds);
                    await Task.Delay(_options.ScreenshotDelay, cancellationToken);
                }

                _logger?.LogInformation("ComputerAgent: Continuing loop after tool execution");

                continue;
            }

            _logger?.LogInformation("ComputerAgent: Breaking loop - no tool calls and no explicit stop");

            break;
        }

        if (iterations >= _options.MaxIterations)
        {
            _logger?.LogWarning("ComputerAgent: Reached max iterations: {MaxIterations}", _options.MaxIterations);

            yield return new AgentResult
            {
                Type = AgentResultType.Error,
                Content = $"Agent reached maximum iterations ({_options.MaxIterations})"
            };
        }

        _logger?.LogInformation("ComputerAgent: Run completed, total iterations={Iterations}", iterations);
    }

    private void AddSystemPrompt()
    {
        // Build system prompt
        string? systemPromptText = !string.IsNullOrEmpty(_options.SystemPrompt)
            ? _options.SystemPrompt
            : $@"You are an AI agent that controls a computer through tool calls.

## CRITICAL RULES - READ CAREFULLY

1. **ALWAYS USE TOOL CALLS** - You MUST respond with tool calls to perform actions. Never output plain text descriptions of what you would do.

2. **NEVER REGURGITATE INPUT DATA** - Do NOT repeat or output the OmniParser element data, bounding boxes, or coordinates as text. Use the data to decide which tool to call, then call the tool.

3. **KEEP WORKING UNTIL DONE** - Continue making tool calls until the task is fully complete. Only provide a final text answer when you have actually accomplished the goal.

## Available Tools
- mouse_move(x, y) - Move mouse to coordinates
- mouse_click(button, x?, y?) - Click at coordinates
- keyboard_type(text) - Type text into focused field
- keyboard_press(key, modifiers?) - Press keys (Enter, Tab, etc.)
- screenshot() - Take a screenshot to see results

## Screen Info
Resolution: {_screenSize.Width}x{_screenSize.Height} pixels. Origin (0,0) is top-left.

## Using OmniParser Data
Screenshots include detected UI elements with coordinates. Use this data to find click targets:
- Each element has: ID, type (text/icon), content, center coordinates
- Use the CENTER coordinates for clicking - they are accurate
- Match elements by content or visual position

## Clicking Guidelines
- Click in the CENTER of elements, not edges
- If a click doesn't work, take a screenshot and try again
- Wait for UI to respond before next action

## Task Execution
1. Look at the screenshot and element data
2. Decide what action to take
3. Call the appropriate tool with coordinates
4. After the action, examine the new screenshot
5. Repeat until task is complete
6. Only then provide a text answer with the result

REMEMBER: Respond with TOOL CALLS, not text descriptions. Never output element lists or coordinate data as text.";

        _llmMessageHistory.Add(new LlmMessage
        {
            Role = "system",
            Content = new List<LlmContent>
            {
                new()
                {
                    Type = "text",
                    Text = systemPromptText
                }
            }
        });
    }

    private List<LlmMessage> FilterScreenshots()
    {
        // Find messages with screenshots, keep only N most recent
        List<LlmMessage> screenshotMessages = _llmMessageHistory
            .Where(m => m.Content.Any(c => c.ImageUrl != null))
            .ToList();

        HashSet<LlmMessage> toOmit = screenshotMessages
            .Take(Math.Max(0, screenshotMessages.Count - _options.OnlyNMostRecentScreenshots))
            .ToHashSet();

        foreach (LlmMessage msg in toOmit)
        {
            msg.Content.RemoveAll(c => c.ImageUrl != null);
            msg.Content.Add(new LlmContent
            {
                Type = "text",
                Text = "[Screenshot omitted for brevity]"
            });
        }

        return _llmMessageHistory;
    }

    /// <summary>
    ///     Creates a user message containing a screenshot with OmniParser data.
    /// </summary>
    private LlmMessage CreateScreenshotMessage(ScreenshotContext ctx)
    {
        var content = new List<LlmContent>
        {
            new()
            {
                Type = "text",
                Text = "Current screenshot:"
            },
            new()
            {
                Type = "image_url",
                ImageUrl = new ImageUrl { Url = $"data:image/png;base64,{ctx.ScreenshotBase64}" }
            }
        };

        if (ctx.OmniParserResult != null)
        {
            content.Add(new LlmContent
            {
                Type = "text",
                Text = "OmniParser annotated (elements highlighted):"
            });
            content.Add(new LlmContent
            {
                Type = "image_url",
                ImageUrl = new ImageUrl { Url = $"data:image/png;base64,{ctx.OmniParserResult.AnnotatedImageBase64}" }
            });

            if (ctx.OmniParserResult.Elements.Count > 0)
            {
                content.Add(new LlmContent
                {
                    Type = "text",
                    Text = $"Detected UI elements:\n{FormatOmniParserElements(ctx.OmniParserResult.Elements)}"
                });
            }
        }

        content.Add(new LlmContent
        {
            Type = "text",
            Text = "Based on the screenshot above, make your next tool call to continue the task. Do NOT output text - respond with a tool call only."
        });

        return new LlmMessage
        {
            Role = "user",
            Content = content
        };
    }

    /// <summary>
    ///     Converts an AgentMessage to LlmMessage format.
    /// </summary>
    private static LlmMessage ConvertToLlmMessage(AgentMessage msg)
    {
        var content = new List<LlmContent>();

        if (!string.IsNullOrEmpty(msg.Content))
        {
            content.Add(new LlmContent
            {
                Type = "text",
                Text = msg.Content
            });
        }

        if (msg.Images != null)
        {
            content.AddRange(msg.Images.Select(img => new LlmContent
            {
                Type = "image_url",
                ImageUrl = new ImageUrl { Url = $"data:{img.MimeType};base64,{img.Base64Data}" }
            }));
        }

        return new LlmMessage
        {
            Role = msg.Role.ToString().ToLowerInvariant(),
            Content = content,
            ToolCalls = msg.ToolCalls,
            ToolCallId = msg.ToolCallId,
            ReasoningDetails = msg.ReasoningDetails
        };
    }

    private static string FormatOmniParserElements(List<OmniParserElement> elements)
    {
        // Full format with type, content, bounds, and center
        var sb = new StringBuilder();
        foreach (OmniParserElement e in elements)
        {
            string content = string.IsNullOrEmpty(e.Content) ? "" : $" \"{e.Content}\"";
            string interactable = e.Interactivity ? "" : " [not clickable]";
            // Format: [id] type "content" bounds=(x1,y1,x2,y2) center=(x,y)
            sb.AppendLine(
                $"[{e.Id}] {e.Type}{content} bounds=({e.BoundingBox[0]},{e.BoundingBox[1]},{e.BoundingBox[2]},{e.BoundingBox[3]}) center=({e.Center[0]},{e.Center[1]}){interactable}");
        }

        return sb.ToString();
    }

    private async Task<AgentMessage> ExecuteToolCall(
        ToolCall toolCall,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger?.LogInformation("ComputerAgent: Executing tool {Tool} with args: {Args}",
                toolCall.Name,
                toolCall.Arguments);

            // Parse tool calls and execute on computer interface
            object result = toolCall.Name switch
            {
                "mouse_move" => await ExecuteMouseMove(toolCall.Arguments, cancellationToken),
                "mouse_click" => await ExecuteMouseClick(toolCall.Arguments, cancellationToken),
                "keyboard_type" => await ExecuteKeyboardType(toolCall.Arguments, cancellationToken),
                "keyboard_press" => await ExecuteKeyboardPress(toolCall.Arguments, cancellationToken),
                // "screenshot" => await ExecuteScreenshot(cancellationToken),
                _ => throw new NotSupportedException($"Unknown tool: {toolCall.Name}")
            };

            string resultJson = JsonSerializer.Serialize(result);
            _logger?.LogInformation("ComputerAgent: Tool {Tool} succeeded: {Result}", toolCall.Name, resultJson);

            return new AgentMessage
            {
                Role = AgentRole.Tool,
                ToolCallId = toolCall.Id,
                Content = resultJson
            };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "ComputerAgent: Tool execution failed: {Tool}", toolCall.Name);

            return new AgentMessage
            {
                Role = AgentRole.Tool,
                ToolCallId = toolCall.Id,
                Content = $"Error: {ex.Message}"
            };
        }
    }

    private async Task<object> ExecuteMouseMove(string argsJson, CancellationToken ct)
    {
        var args = JsonSerializer.Deserialize<MouseMoveArgs>(argsJson, JsonOptions);
        if (args == null)
        {
            throw new ArgumentException("Invalid mouse_move arguments");
        }

        await _computer.Interface.Mouse.Move(args.X, args.Y, ct);

        return new
        {
            success = true,
            x = args.X,
            y = args.Y
        };
    }

    private async Task<object> ExecuteMouseClick(string argsJson, CancellationToken ct)
    {
        var args = JsonSerializer.Deserialize<MouseClickArgs>(argsJson, JsonOptions);
        if (args == null)
        {
            throw new ArgumentException("Invalid mouse_click arguments");
        }

        MouseButton button = args.Button?.ToLowerInvariant() switch
        {
            "right" => MouseButton.Right,
            "middle" => MouseButton.Middle,
            _ => MouseButton.Left
        };

        switch (button)
        {
            case MouseButton.Right:
                await _computer.Interface.Mouse.RightClick(args.X, args.Y, ct);

                break;
            case MouseButton.Middle:
                await _computer.Interface.Mouse.Down(args.X, args.Y, MouseButton.Middle, ct);
                await _computer.Interface.Mouse.Up(args.X, args.Y, MouseButton.Middle, ct);

                break;
            default:
                await _computer.Interface.Mouse.LeftClick(args.X, args.Y, ct);

                break;
        }

        return new
        {
            success = true,
            button = button.ToString()
        };
    }

    private async Task<object> ExecuteKeyboardType(string argsJson, CancellationToken ct)
    {
        var args = JsonSerializer.Deserialize<KeyboardTypeArgs>(argsJson, JsonOptions);
        if (args == null)
        {
            throw new ArgumentException("Invalid keyboard_type arguments");
        }

        await _computer.Interface.Keyboard.Type(args.Text, ct);

        return new
        {
            success = true,
            text = args.Text
        };
    }

    private async Task<object> ExecuteKeyboardPress(string argsJson, CancellationToken ct)
    {
        var args = JsonSerializer.Deserialize<KeyboardPressArgs>(argsJson, JsonOptions);
        if (args == null)
        {
            throw new ArgumentException("Invalid keyboard_press arguments");
        }

        // Build key combination
        var keys = new List<string>();
        if (args.Modifiers != null)
        {
            keys.AddRange(args.Modifiers);
        }

        keys.Add(args.Key);

        string keyCombo = string.Join("+", keys);

        // Use Hotkey when there are modifiers, Press for single keys
        if (keys.Count > 1)
        {
            await _computer.Interface.Keyboard.Hotkey(ct, keys.ToArray());
        }
        else
        {
            await _computer.Interface.Keyboard.Press(args.Key, ct);
        }

        return new
        {
            success = true,
            key = keyCombo
        };
    }

    private async Task<object> ExecuteScreenshot(CancellationToken ct)
    {
        byte[] screenshot = await _computer.Interface.Screen.Screenshot(ct);
        string base64 = Convert.ToBase64String(screenshot);

        return new
        {
            success = true,
            screenshot = base64
        };
    }

    /// <summary>
    ///     Takes a screenshot, runs OmniParser if enabled, adds to history, and returns the context.
    /// </summary>
    private async Task<ScreenshotContext> CaptureScreenshotAsync(CancellationToken cancellationToken)
    {
        // Take screenshot
        byte[] screenshot = await _computer.Interface.Screen.Screenshot(cancellationToken);
        _screenSize = await _computer.Interface.Screen.GetSize(cancellationToken);
        string screenshotBase64 = Convert.ToBase64String(screenshot);

        _logger?.LogInformation("ComputerAgent: Screenshot taken, size={Width}x{Height}, bytes={Bytes}",
            _screenSize.Width,
            _screenSize.Height,
            screenshot.Length);

        // Run OmniParser if enabled
        OmniParserResult? omniParserResult = null;
        if (_omniParser != null)
        {
            try
            {
                _logger?.LogInformation("ComputerAgent: Running OmniParser analysis...");
                omniParserResult = await _omniParser.ParseAsync(
                    screenshotBase64,
                    _screenSize.Width,
                    _screenSize.Height,
                    _options.OmniParserBoxThreshold,
                    _options.OmniParserIouThreshold,
                    cancellationToken);
                _logger?.LogInformation("ComputerAgent: OmniParser detected {Count} UI elements",
                    omniParserResult.Elements.Count);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "ComputerAgent: OmniParser failed, continuing without element detection");
            }
        }

        // Return context (history is now managed in _llmMessageHistory)
        return new ScreenshotContext(screenshotBase64, omniParserResult);
    }

    /// <summary>
    ///     Holds a screenshot with its optional OmniParser analysis.
    /// </summary>
    private record ScreenshotContext(
        string ScreenshotBase64,
        OmniParserResult? OmniParserResult
    );

    // Tool argument models
    private class MouseMoveArgs
    {
        public int X { get; set; }
        public int Y { get; set; }
    }

    private class MouseClickArgs
    {
        public string? Button { get; set; }
        public int? X { get; set; }
        public int? Y { get; set; }
    }

    private class KeyboardTypeArgs
    {
        public required string Text { get; set; }
    }

    private class KeyboardPressArgs
    {
        public required string Key { get; set; }
        public List<string>? Modifiers { get; set; }
    }
}
