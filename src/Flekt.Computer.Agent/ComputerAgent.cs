using System.Runtime.CompilerServices;
using System.Text.Json;
using Flekt.Computer.Agent.Models;
using Flekt.Computer.Agent.Providers;
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
        _logger?.LogInformation("Agent run started with {Count} messages", messageList.Count);

        var iterations = 0;

        while (!cancellationToken.IsCancellationRequested && iterations < _options.MaxIterations)
        {
            iterations++;
            _logger?.LogDebug("Agent iteration {Iteration} started", iterations);

            // Take screenshot for vision context
            byte[] screenshot = await _computer.Interface.Screen.Screenshot(cancellationToken);
            var screenSize = await _computer.Interface.Screen.GetSize(cancellationToken);
            
            // Convert to base64 for LLM
            string screenshotBase64 = Convert.ToBase64String(screenshot);
            
            // Add to history and maintain only N most recent
            _screenshotHistory.Add(screenshotBase64);
            if (_screenshotHistory.Count > _options.OnlyNMostRecentScreenshots)
            {
                _screenshotHistory.RemoveAt(0);
            }
            
            // Prepare messages with screenshot context
            var contextMessages = PrepareMessages(messageList, screenshotBase64, screenSize);
            
            // Call LLM with streaming
            bool hasToolCalls = false;
            await foreach (var chunk in _llmProvider.StreamChatAsync(contextMessages, cancellationToken))
            {
                // Yield reasoning/thinking
                if (chunk.Type == AgentResultType.Reasoning)
                {
                    yield return chunk;
                    continue;
                }

                // Execute tool calls (computer actions)
                if (chunk.Type == AgentResultType.ToolCall && chunk.ToolCall != null)
                {
                    hasToolCalls = true;
                    yield return chunk;
                    
                    var toolResult = await ExecuteToolCall(chunk.ToolCall, cancellationToken);
                    messageList.Add(toolResult);
                    
                    // Take screenshot after action
                    if (_options.ScreenshotAfterAction)
                    {
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

                // Final message from assistant
                if (chunk.Type == AgentResultType.Message)
                {
                    yield return chunk;
                    
                    if (!string.IsNullOrEmpty(chunk.Content))
                    {
                        messageList.Add(new AgentMessage
                        {
                            Role = AgentRole.Assistant,
                            Content = chunk.Content
                        });
                    }
                    
                    // Check if we should stop
                    if (!chunk.ContinueLoop && !hasToolCalls)
                    {
                        _logger?.LogInformation("Agent run completed after {Iterations} iterations", iterations);
                        yield break;
                    }
                }
            }

            // If we had tool calls, continue the loop to send results back to LLM
            if (hasToolCalls)
            {
                continue;
            }

            // If no tool calls and no explicit stop, break
            break;
        }

        if (iterations >= _options.MaxIterations)
        {
            _logger?.LogWarning("Agent reached max iterations: {MaxIterations}", _options.MaxIterations);
            yield return new AgentResult
            {
                Type = AgentResultType.Error,
                Content = $"Agent reached maximum iterations ({_options.MaxIterations})"
            };
        }
    }

    private List<LlmMessage> PrepareMessages(
        List<AgentMessage> messages,
        string currentScreenshot,
        ScreenSize screenSize)
    {
        var llmMessages = new List<LlmMessage>();

        // Add system prompt if provided
        if (!string.IsNullOrEmpty(_options.SystemPrompt))
        {
            llmMessages.Add(new LlmMessage
            {
                Role = "system",
                Content = new List<LlmContent>
                {
                    new() { Type = "text", Text = _options.SystemPrompt }
                }
            });
        }
        else
        {
            // Default system prompt
            llmMessages.Add(new LlmMessage
            {
                Role = "system",
                Content = new List<LlmContent>
                {
                    new() { Type = "text", Text = $@"You are an AI agent that can control a computer through vision and actions.

You can see the screen and perform actions like:
- mouse_move(x, y) - Move mouse to coordinates
- mouse_click(button, x?, y?) - Click mouse button
- keyboard_type(text) - Type text
- keyboard_press(key, modifiers?) - Press a key
- screenshot() - Take a screenshot

The screen size is {screenSize.Width}x{screenSize.Height} pixels.

When given a task:
1. Observe the current screen carefully
2. Plan your actions step by step
3. Execute actions to complete the task
4. Verify the results

Be precise with coordinates and actions. If something doesn't work, try a different approach." }
                }
            });
        }

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

        // Add current screenshot as user message
        llmMessages.Add(new LlmMessage
        {
            Role = "user",
            Content = new List<LlmContent>
            {
                new()
                {
                    Type = "image_url",
                    ImageUrl = new ImageUrl
                    {
                        Url = $"data:image/png;base64,{currentScreenshot}"
                    }
                }
            }
        });

        return llmMessages;
    }

    private async Task<AgentMessage> ExecuteToolCall(
        ToolCall toolCall,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger?.LogInformation("Executing tool: {Tool} with args: {Args}", 
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

            return new AgentMessage
            {
                Role = AgentRole.Tool,
                ToolCallId = toolCall.Id,
                Content = JsonSerializer.Serialize(result)
            };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Tool execution failed: {Tool}", toolCall.Name);
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
        var args = JsonSerializer.Deserialize<MouseMoveArgs>(argsJson);
        if (args == null) throw new ArgumentException("Invalid mouse_move arguments");

        await _computer.Interface.Mouse.MoveTo(args.X, args.Y, ct);
        
        return new { success = true, x = args.X, y = args.Y };
    }

    private async Task<object> ExecuteMouseClick(string argsJson, CancellationToken ct)
    {
        var args = JsonSerializer.Deserialize<MouseClickArgs>(argsJson);
        if (args == null) throw new ArgumentException("Invalid mouse_click arguments");

        var button = args.Button?.ToLowerInvariant() switch
        {
            "right" => MouseButton.Right,
            "middle" => MouseButton.Middle,
            _ => MouseButton.Left
        };

        if (args.X.HasValue && args.Y.HasValue)
        {
            await _computer.Interface.Mouse.MoveTo(args.X.Value, args.Y.Value, ct);
        }

        await _computer.Interface.Mouse.Click(button, ct);
        
        return new { success = true, button = button.ToString() };
    }

    private async Task<object> ExecuteKeyboardType(string argsJson, CancellationToken ct)
    {
        var args = JsonSerializer.Deserialize<KeyboardTypeArgs>(argsJson);
        if (args == null) throw new ArgumentException("Invalid keyboard_type arguments");

        await _computer.Interface.Keyboard.Type(args.Text, ct);
        
        return new { success = true, text = args.Text };
    }

    private async Task<object> ExecuteKeyboardPress(string argsJson, CancellationToken ct)
    {
        var args = JsonSerializer.Deserialize<KeyboardPressArgs>(argsJson);
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

