using System.Runtime.CompilerServices;
using System.Text.Json;
using Flekt.Computer.Agent.Models;
using Flekt.Computer.Agent.Providers;
using Flekt.Computer.Agent.Services;
using Flekt.Computer.Abstractions;
using Microsoft.Extensions.Logging;

namespace Flekt.Computer.Agent;

/// <summary>
/// AI agent that can control computers using vision-language models.
/// Inspired by CUA's agent SDK with async foreach streaming pattern.
/// </summary>
public sealed class ComputerAgent : IAsyncDisposable
{
    private readonly IComputer _computer;
    private readonly ILlmProvider _llmProvider;
    private readonly ComputerAgentOptions _options;
    private readonly ILogger<ComputerAgent>? _logger;
    private readonly List<ScreenshotContext> _screenshotHistory = new();
    private readonly IOmniParser? _omniParser;
    private ScreenSize _screenSize;

    /// <summary>
    /// Holds a screenshot with its optional OmniParser analysis.
    /// </summary>
    private record ScreenshotContext(
        string ScreenshotBase64,
        OmniParserResult? OmniParserResult);

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

        // Phase 1: Only OpenRouter supported
        // Model format: "anthropic/claude-3.5-sonnet", "openai/gpt-4o", "google/gemini-pro-vision"
        // OpenRouter handles routing to the appropriate provider
        _llmProvider = new OpenRouterProvider(model, apiKey, loggerFactory?.CreateLogger<OpenRouterProvider>());

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

    /// <summary>
    /// Run the agent with streaming results (async foreach pattern like CUA).
    /// </summary>
    /// <param name="messages">Conversation history</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Async enumerable of agent results</returns>
    public async IAsyncEnumerable<AgentResult> RunAsync(
        IEnumerable<AgentMessage> messages,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var messageList = messages.ToList();
        _logger?.LogInformation("ComputerAgent: Run started with {Count} messages", messageList.Count);

        var iterations = 0;

        while (!cancellationToken.IsCancellationRequested && iterations < _options.MaxIterations)
        {
            iterations++;
            _logger?.LogInformation("ComputerAgent: Iteration {Iteration}/{MaxIterations} started", iterations, _options.MaxIterations);

            // Take screenshot for vision context (adds to history automatically)
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

            // Yield the screenshot so callers can see/save it (outside try-catch due to C# yield limitations)
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
                    }).ToList()
            };

            // Prepare messages with all recent screenshots from history
            var contextMessages = PrepareMessages(messageList);
            _logger?.LogInformation("ComputerAgent: Prepared {Count} messages for LLM (including {ScreenshotCount} screenshots)",
                contextMessages.Count, _screenshotHistory.Count);

            // Call LLM with streaming
            // Collect tool calls first, then execute them after we have the full response
            var pendingToolCalls = new List<ToolCall>();
            string? assistantContent = null;
            bool continueLoop = false;
            System.Text.Json.JsonElement? reasoningDetails = null;

            _logger?.LogInformation("ComputerAgent: Calling LLM...");
            await foreach (var chunk in _llmProvider.StreamChatAsync(contextMessages, cancellationToken))
            {
                // Yield reasoning/thinking
                if (chunk.Type == AgentResultType.Reasoning)
                {
                    yield return chunk;
                    continue;
                }

                // Collect tool calls (don't execute yet - need to add assistant message first)
                if (chunk.Type == AgentResultType.ToolCall && chunk.ToolCall != null)
                {
                    pendingToolCalls.Add(chunk.ToolCall);
                    yield return chunk;
                }

                // Final message from assistant
                if (chunk.Type == AgentResultType.Message)
                {
                    assistantContent = chunk.Content;
                    continueLoop = chunk.ContinueLoop;
                    reasoningDetails = chunk.ReasoningDetails;
                    yield return chunk;
                }
            }

            // Now process the response: add assistant message with tool calls, then execute tools
            bool hasToolCalls = pendingToolCalls.Count > 0;
            _logger?.LogInformation("ComputerAgent: LLM response received - ToolCalls={ToolCallCount}, HasContent={HasContent}, ContinueLoop={ContinueLoop}",
                pendingToolCalls.Count, !string.IsNullOrEmpty(assistantContent), continueLoop);

            if (hasToolCalls)
            {
                // Add assistant message WITH tool calls to history (required by OpenAI API)
                // Include reasoning_details for Gemini models
                messageList.Add(new AgentMessage
                {
                    Role = AgentRole.Assistant,
                    Content = assistantContent,
                    ToolCalls = pendingToolCalls,
                    ReasoningDetails = reasoningDetails
                });

                // Execute each tool call and add results
                foreach (var toolCall in pendingToolCalls)
                {
                    _logger?.LogInformation("ComputerAgent: Executing tool {ToolName} (id={ToolId})", toolCall.Name, toolCall.Id);
                    var toolResult = await ExecuteToolCall(toolCall, cancellationToken);
                    messageList.Add(toolResult);
                    _logger?.LogInformation("ComputerAgent: Tool {ToolName} executed, result added to messages", toolCall.Name);

                }
                _logger?.LogInformation("ComputerAgent: All {Count} tool calls executed, continuing loop", pendingToolCalls.Count);
            }
            else if (!string.IsNullOrEmpty(assistantContent))
            {
                // No tool calls - just add the assistant message
                messageList.Add(new AgentMessage
                {
                    Role = AgentRole.Assistant,
                    Content = assistantContent,
                    ReasoningDetails = reasoningDetails
                });
                _logger?.LogInformation("ComputerAgent: Added assistant message to history (no tool calls)");
            }

            // Check if we should stop
            if (!continueLoop && !hasToolCalls)
            {
                _logger?.LogInformation("ComputerAgent: Stopping - no tool calls and continueLoop=false after {Iterations} iterations", iterations);
                yield break;
            }

            // If we had tool calls, continue the loop to send results back to LLM
            if (hasToolCalls)
            {
                _logger?.LogInformation("ComputerAgent: Continuing loop after tool execution");
                continue;
            }

            // If no tool calls and no explicit stop, break
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

    private List<LlmMessage> PrepareMessages(List<AgentMessage> messages)
    {
        var llmMessages = new List<LlmMessage>();

        // Build the system prompt
        string systemPromptText;
        if (!string.IsNullOrEmpty(_options.SystemPrompt))
        {
            systemPromptText = _options.SystemPrompt;
        }
        else
        {
            // Default system prompt with coordinate guidance (based on CUA library)
            systemPromptText = $@"You are an AI agent that can control a computer through vision and actions.

You can see the screen and perform actions like:
- mouse_move(x, y) - Move mouse to coordinates
- mouse_click(button, x?, y?) - Click mouse button
- keyboard_type(text) - Type text
- keyboard_press(key, modifiers?) - Press a key
- screenshot() - Take a screenshot

The screen resolution is {_screenSize.Width}x{_screenSize.Height} pixels. Coordinates start at (0,0) in the top-left corner.

## Using OmniParser UI Detection
Screenshots may include OmniParser analysis with:
1. An annotated image showing detected UI elements with numbered bounding boxes
2. A list of detected elements with their coordinates

When OmniParser data is provided:
* Each element has an ID, type (text/icon), content description, and interactivity flag
* Use the CENTER coordinates provided for each element - these are precise click targets
* Match the element you want to interact with to one in the list by its content or visual position
* The annotated image numbers correspond to element IDs in the list

IMPORTANT GUIDELINES FOR CLICKING:
* Always click in the CENTER of UI elements (buttons, icons, links, text fields), never on their edges.
* When OmniParser coordinates are provided, use them directly - they are accurate.
* If no OmniParser data is available, carefully examine the screenshot to estimate coordinates.
* If a click doesn't work, try adjusting your coordinates so the cursor tip is precisely on the target element.
* Some applications may take time to respond - if nothing happens after clicking, wait and take another screenshot before trying again.

When given a task:
1. Carefully observe the current screenshot and any OmniParser element data
2. Find the target element in the detected elements list, or estimate coordinates from the image
3. Execute the action using the CENTER coordinates
4. Take a screenshot to verify the result before proceeding

Remember: Precision is critical. A click that is off by even 20-30 pixels may miss the target element entirely.";
        }

        llmMessages.Add(new LlmMessage
        {
            Role = "system",
            Content = new List<LlmContent>
            {
                new() { Type = "text", Text = systemPromptText }
            }
        });

        // Convert agent messages to LLM messages
        foreach (var msg in messages)
        {
            var llmMsg = new LlmMessage
            {
                Role = msg.Role.ToString().ToLowerInvariant(),
                Content = new List<LlmContent>()
            };

            if (!string.IsNullOrEmpty(msg.Content))
            {
                llmMsg.Content.Add(new LlmContent
                {
                    Type = "text",
                    Text = msg.Content
                });
            }

            if (msg.Images != null)
            {
                foreach (var img in msg.Images)
                {
                    llmMsg.Content.Add(new LlmContent
                    {
                        Type = "image_url",
                        ImageUrl = new ImageUrl
                        {
                            Url = $"data:{img.MimeType};base64,{img.Base64Data}"
                        }
                    });
                }
            }

            if (msg.ToolCalls != null)
            {
                llmMsg.ToolCalls = msg.ToolCalls;
            }

            if (!string.IsNullOrEmpty(msg.ToolCallId))
            {
                llmMsg.ToolCallId = msg.ToolCallId;
            }

            // Pass through reasoning_details for Gemini models
            if (msg.ReasoningDetails.HasValue)
            {
                llmMsg.ReasoningDetails = msg.ReasoningDetails;
            }

            llmMessages.Add(llmMsg);
        }

        // Add all recent screenshots from history as a user message
        var screenshotContent = new List<LlmContent>();

        for (int i = 0; i < _screenshotHistory.Count; i++)
        {
            var ctx = _screenshotHistory[i];
            var isLatest = i == _screenshotHistory.Count - 1;
            var label = isLatest ? "Current screenshot" : $"Previous screenshot ({_screenshotHistory.Count - 1 - i} actions ago)";

            // Add the original screenshot
            screenshotContent.Add(new LlmContent
            {
                Type = "text",
                Text = $"{label}:"
            });
            screenshotContent.Add(new LlmContent
            {
                Type = "image_url",
                ImageUrl = new ImageUrl
                {
                    Url = $"data:image/png;base64,{ctx.ScreenshotBase64}"
                }
            });

            // Add annotated version and element data if OmniParser result available
            if (ctx.OmniParserResult != null)
            {
                screenshotContent.Add(new LlmContent
                {
                    Type = "text",
                    Text = $"{label} (OmniParser annotated - elements highlighted):"
                });
                screenshotContent.Add(new LlmContent
                {
                    Type = "image_url",
                    ImageUrl = new ImageUrl
                    {
                        Url = $"data:image/png;base64,{ctx.OmniParserResult.AnnotatedImageBase64}"
                    }
                });

                // Add bounding box data for this screenshot
                if (ctx.OmniParserResult.Elements.Count > 0)
                {
                    screenshotContent.Add(new LlmContent
                    {
                        Type = "text",
                        Text = $"Detected UI elements for {label.ToLower()}:\n{FormatOmniParserElements(ctx.OmniParserResult.Elements)}"
                    });
                }
            }
        }

        llmMessages.Add(new LlmMessage
        {
            Role = "user",
            Content = screenshotContent
        });

        return llmMessages;
    }

    private static string FormatOmniParserElements(List<OmniParserElement> elements)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var e in elements)
        {
            sb.AppendLine($"  - Element {e.Id}: \"{e.Content}\" (type={e.Type}, interactable={e.Interactivity})");
            sb.AppendLine($"    Bounding box: ({e.BoundingBox[0]}, {e.BoundingBox[1]}) to ({e.BoundingBox[2]}, {e.BoundingBox[3]}), Center: ({e.Center[0]}, {e.Center[1]})");
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
                toolCall.Name, toolCall.Arguments);

            // Parse tool calls and execute on computer interface
            var result = toolCall.Name switch
            {
                "mouse_move" => await ExecuteMouseMove(toolCall.Arguments, cancellationToken),
                "mouse_click" => await ExecuteMouseClick(toolCall.Arguments, cancellationToken),
                "keyboard_type" => await ExecuteKeyboardType(toolCall.Arguments, cancellationToken),
                "keyboard_press" => await ExecuteKeyboardPress(toolCall.Arguments, cancellationToken),
                "screenshot" => await ExecuteScreenshot(cancellationToken),
                _ => throw new NotSupportedException($"Unknown tool: {toolCall.Name}")
            };

            var resultJson = JsonSerializer.Serialize(result);
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
        if (args == null) throw new ArgumentException("Invalid mouse_move arguments");

        await _computer.Interface.Mouse.Move(args.X, args.Y, ct);

        return new { success = true, x = args.X, y = args.Y };
    }

    private async Task<object> ExecuteMouseClick(string argsJson, CancellationToken ct)
    {
        var args = JsonSerializer.Deserialize<MouseClickArgs>(argsJson, JsonOptions);
        if (args == null) throw new ArgumentException("Invalid mouse_click arguments");

        var button = args.Button?.ToLowerInvariant() switch
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
        
        return new { success = true, button = button.ToString() };
    }

    private async Task<object> ExecuteKeyboardType(string argsJson, CancellationToken ct)
    {
        var args = JsonSerializer.Deserialize<KeyboardTypeArgs>(argsJson, JsonOptions);
        if (args == null) throw new ArgumentException("Invalid keyboard_type arguments");

        await _computer.Interface.Keyboard.Type(args.Text, ct);

        return new { success = true, text = args.Text };
    }

    private async Task<object> ExecuteKeyboardPress(string argsJson, CancellationToken ct)
    {
        var args = JsonSerializer.Deserialize<KeyboardPressArgs>(argsJson, JsonOptions);
        if (args == null) throw new ArgumentException("Invalid keyboard_press arguments");

        // Build key combination
        var keys = new List<string>();
        if (args.Modifiers != null)
        {
            keys.AddRange(args.Modifiers);
        }
        keys.Add(args.Key);

        var keyCombo = string.Join("+", keys);
        await _computer.Interface.Keyboard.Press(keyCombo, ct);
        
        return new { success = true, key = keyCombo };
    }

    private async Task<object> ExecuteScreenshot(CancellationToken ct)
    {
        var screenshot = await _computer.Interface.Screen.Screenshot(ct);
        var base64 = Convert.ToBase64String(screenshot);
        
        return new { success = true, screenshot = base64 };
    }

    /// <summary>
    /// Takes a screenshot, runs OmniParser if enabled, adds to history, and returns the context.
    /// </summary>
    private async Task<ScreenshotContext> CaptureScreenshotAsync(CancellationToken cancellationToken)
    {
        // Take screenshot
        var screenshot = await _computer.Interface.Screen.Screenshot(cancellationToken);
        _screenSize = await _computer.Interface.Screen.GetSize(cancellationToken);
        var screenshotBase64 = Convert.ToBase64String(screenshot);

        _logger?.LogInformation("ComputerAgent: Screenshot taken, size={Width}x{Height}, bytes={Bytes}",
            _screenSize.Width, _screenSize.Height, screenshot.Length);

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

        // Create context and add to history
        var context = new ScreenshotContext(screenshotBase64, omniParserResult);
        _screenshotHistory.Add(context);

        // Maintain only N most recent screenshots
        while (_screenshotHistory.Count > _options.OnlyNMostRecentScreenshots)
        {
            _screenshotHistory.RemoveAt(0);
        }

        return context;
    }

    public async ValueTask DisposeAsync()
    {
        if (_llmProvider is IAsyncDisposable disposable)
        {
            await disposable.DisposeAsync();
        }
    }

    // JSON options for case-insensitive deserialization (LLMs may use lowercase property names)
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

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

