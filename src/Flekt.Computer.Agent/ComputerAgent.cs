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
    private readonly List<string> _screenshotHistory = new();
    private readonly IOmniParser? _omniParser;

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

            // Take screenshot for vision context
            _logger?.LogDebug("ComputerAgent: Taking screenshot...");
            byte[] screenshot;
            ScreenSize screenSize;
            try
            {
                screenshot = await _computer.Interface.Screen.Screenshot(cancellationToken);
                screenSize = await _computer.Interface.Screen.GetSize(cancellationToken);
                _logger?.LogInformation("ComputerAgent: Screenshot taken, size={Width}x{Height}, bytes={Bytes}",
                    screenSize.Width, screenSize.Height, screenshot.Length);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "ComputerAgent: Failed to take screenshot");
                throw;
            }

            // Convert to base64 for LLM
            string screenshotBase64 = Convert.ToBase64String(screenshot);

            // Run OmniParser if enabled
            OmniParserResult? omniParserResult = null;
            if (_omniParser != null)
            {
                try
                {
                    _logger?.LogInformation("ComputerAgent: Running OmniParser analysis...");
                    omniParserResult = await _omniParser.ParseAsync(
                        screenshotBase64,
                        screenSize.Width,
                        screenSize.Height,
                        cancellationToken);
                    _logger?.LogInformation("ComputerAgent: OmniParser detected {Count} UI elements",
                        omniParserResult.Elements.Count);
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "ComputerAgent: OmniParser failed, continuing without element detection");
                }
            }

            // Add to history and maintain only N most recent
            _screenshotHistory.Add(screenshotBase64);
            if (_screenshotHistory.Count > _options.OnlyNMostRecentScreenshots)
            {
                _screenshotHistory.RemoveAt(0);
            }

            // Prepare messages with screenshot context
            var contextMessages = PrepareMessages(messageList, screenshotBase64, screenSize, omniParserResult);
            _logger?.LogInformation("ComputerAgent: Prepared {Count} messages for LLM (including screenshot)", contextMessages.Count);

            // Call LLM with streaming
            // Collect tool calls first, then execute them after we have the full response
            var pendingToolCalls = new List<ToolCall>();
            string? assistantContent = null;
            bool continueLoop = false;

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
                messageList.Add(new AgentMessage
                {
                    Role = AgentRole.Assistant,
                    Content = assistantContent,
                    ToolCalls = pendingToolCalls
                });

                // Execute each tool call and add results
                foreach (var toolCall in pendingToolCalls)
                {
                    _logger?.LogInformation("ComputerAgent: Executing tool {ToolName} (id={ToolId})", toolCall.Name, toolCall.Id);
                    var toolResult = await ExecuteToolCall(toolCall, cancellationToken);
                    messageList.Add(toolResult);
                    _logger?.LogInformation("ComputerAgent: Tool {ToolName} executed, result added to messages", toolCall.Name);

                    // Take screenshot after action
                    if (_options.ScreenshotAfterAction)
                    {
                        _logger?.LogDebug("ComputerAgent: Taking post-action screenshot...");
                        await Task.Delay(_options.ScreenshotDelay, cancellationToken);
                        screenshot = await _computer.Interface.Screen.Screenshot(cancellationToken);
                        screenshotBase64 = Convert.ToBase64String(screenshot);

                        yield return new AgentResult
                        {
                            Type = AgentResultType.Screenshot,
                            Screenshot = screenshotBase64
                        };
                    }
                }
                _logger?.LogInformation("ComputerAgent: All {Count} tool calls executed, continuing loop", pendingToolCalls.Count);
            }
            else if (!string.IsNullOrEmpty(assistantContent))
            {
                // No tool calls - just add the assistant message
                messageList.Add(new AgentMessage
                {
                    Role = AgentRole.Assistant,
                    Content = assistantContent
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

    private List<LlmMessage> PrepareMessages(
        List<AgentMessage> messages,
        string currentScreenshot,
        ScreenSize screenSize,
        OmniParserResult? omniParserResult = null)
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

The screen resolution is {screenSize.Width}x{screenSize.Height} pixels. Coordinates start at (0,0) in the top-left corner.";

            // Add OmniParser context if available
            if (omniParserResult != null && omniParserResult.Elements.Count > 0)
            {
                systemPromptText += @"

## Detected UI Elements (from OmniParser)
You are provided with two images:
1. The original screenshot
2. An annotated version with detected UI elements highlighted and numbered

The following UI elements have been detected with their bounding boxes and center coordinates:

";
                systemPromptText += FormatOmniParserElements(omniParserResult.Elements);

                systemPromptText += @"

IMPORTANT GUIDELINES FOR CLICKING:
* Use the detected UI elements above to find precise coordinates
* When clicking an element, use the CENTER coordinates provided for that element
* Match the requested element to one in the list and use its center coordinates directly
* The annotated image shows element numbers that correspond to the list above
* Coordinates must be integers within the image bounds";
            }
            else
            {
                systemPromptText += @"

IMPORTANT GUIDELINES FOR CLICKING:
* Whenever you intend to click on an element, carefully examine the screenshot to determine the EXACT coordinates of the element's center before clicking.
* Always click in the CENTER of UI elements (buttons, icons, links, text fields), never on their edges.
* If a click doesn't work, try adjusting your coordinates so the cursor tip is precisely on the target element.
* Some applications may take time to respond - if nothing happens after clicking, wait and take another screenshot before trying again.
* For small elements like checkboxes, radio buttons, or close buttons, be extra precise with coordinates.";
            }

            systemPromptText += @"

When given a task:
1. Carefully observe the current screenshot
2. Identify the exact pixel coordinates of the element you need to interact with
3. Execute the action with precise coordinates
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

            llmMessages.Add(llmMsg);
        }

        // Add current screenshot(s) as user message
        var screenshotContent = new List<LlmContent>();

        if (omniParserResult != null)
        {
            // Include both original and annotated images when OmniParser is available
            screenshotContent.Add(new LlmContent
            {
                Type = "text",
                Text = "Here is the original screenshot:"
            });
            screenshotContent.Add(new LlmContent
            {
                Type = "image_url",
                ImageUrl = new ImageUrl
                {
                    Url = $"data:image/png;base64,{currentScreenshot}"
                }
            });
            screenshotContent.Add(new LlmContent
            {
                Type = "text",
                Text = "Here is the OmniParser annotated version with detected elements highlighted:"
            });
            screenshotContent.Add(new LlmContent
            {
                Type = "image_url",
                ImageUrl = new ImageUrl
                {
                    Url = $"data:image/png;base64,{omniParserResult.AnnotatedImageBase64}"
                }
            });
        }
        else
        {
            // Just the original screenshot
            screenshotContent.Add(new LlmContent
            {
                Type = "image_url",
                ImageUrl = new ImageUrl
                {
                    Url = $"data:image/png;base64,{currentScreenshot}"
                }
            });
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

