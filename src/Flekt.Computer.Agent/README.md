# Flekt.Computer.Agent

AI Agent SDK for building vision-language model agents that can control computers.

## Features

- 🤖 **OpenRouter Integration**: Access multiple LLMs through a single API (Anthropic, OpenAI, Google, and more)
- 👁️ **Vision Support**: Automatic screenshot injection for VLMs
- 🔧 **Tool Calling**: Computer control via structured tool calls
- 🌊 **Streaming**: Async foreach pattern for real-time results
- 💰 **Cost Tracking**: Track token usage and costs
- 🎯 **Type-Safe**: Fully typed C# API

## Quick Start

### Installation

```bash
dotnet add package Flekt.Computer.Agent
```

### Basic Usage

```csharp
using Flekt.Computer;
using Flekt.Computer.Agent;

// Create a computer session
await using var computer = await Computer.CreateAsync(new ComputerOptions
{
    EnvironmentId = Guid.Parse("your-env-id"),
    Provider = ProviderType.Cloud,
    ApiKey = "your-flekt-api-key"
});

// Create an AI agent (OpenRouter only in Phase 1)
var agent = new ComputerAgent(
    model: "anthropic/claude-3.5-sonnet", // OpenRouter model format
    computer: computer,
    apiKey: "your-openrouter-api-key"
);

// Run the agent with streaming results
var messages = new[]
{
    new AgentMessage
    {
        Role = AgentRole.User,
        Content = "Open Notepad and type 'Hello from AI!'"
    }
};

await foreach (var result in agent.RunAsync(messages))
{
    switch (result.Type)
    {
        case AgentResultType.Reasoning:
            Console.WriteLine($"[Thinking] {result.Content}");
            break;
            
        case AgentResultType.ToolCall:
            Console.WriteLine($"[Action] {result.ToolCall.Name}");
            break;
            
        case AgentResultType.Message:
            Console.WriteLine($"[Response] {result.Content}");
            break;
    }
}
```

### Supported Providers

**Phase 1 (Current)**: OpenRouter only

| Provider | Model String Example | Vision | Tools | Streaming |
|----------|---------------------|--------|-------|-----------|
| OpenRouter | `anthropic/claude-3.5-sonnet` | ✅ | ✅ | ✅ |
| OpenRouter | `openai/gpt-4o` | ✅ | ✅ | ✅ |
| OpenRouter | `google/gemini-pro-vision` | ✅ | ✅ | ✅ |

### Advanced Configuration

```csharp
var options = new ComputerAgentOptions
{
    // Take screenshot after each action (default: true)
    ScreenshotAfterAction = true,
    
    // Delay before screenshot to let UI settle
    ScreenshotDelay = TimeSpan.FromMilliseconds(500),
    
    // Maximum iterations before stopping
    MaxIterations = 100,
    
    // Keep only N most recent screenshots in context
    OnlyNMostRecentScreenshots = 3,
    
    // Custom system prompt
    SystemPrompt = "You are a helpful assistant..."
};

var agent = new ComputerAgent(
    model: "anthropic/claude-3.5-sonnet",
    computer: computer,
    apiKey: apiKey,
    options: options
);
```

## Architecture

### Async Foreach Pattern (like CUA)

The SDK uses C#'s `IAsyncEnumerable<T>` for streaming results:

```csharp
await foreach (var result in agent.RunAsync(messages))
{
    // Process results as they arrive
    // State is managed by caller
}
```

This pattern:
- ✅ Allows real-time processing of agent actions
- ✅ Enables streaming UI updates
- ✅ Gives caller full control over state
- ✅ Supports cancellation via CancellationToken

### State Management

- **SDK**: Stateless - caller manages conversation history
- **Web App**: Server-side persistence via SignalR + database

## Available Tools

The agent has access to the following computer control tools:

### Mouse Tools
- `mouse_move(x, y)` - Move mouse cursor to coordinates
- `mouse_click(button?, x?, y?)` - Click mouse button (left/right/middle)

### Keyboard Tools
- `keyboard_type(text)` - Type text
- `keyboard_press(key, modifiers?)` - Press key with optional modifiers (Ctrl, Alt, Shift)

### Screen Tools
- `screenshot()` - Take a screenshot

## Examples

### Example 1: Simple Task

```csharp
var messages = new[]
{
    new AgentMessage
    {
        Role = AgentRole.User,
        Content = "Click the Start button"
    }
};

await foreach (var result in agent.RunAsync(messages))
{
    if (result.Type == AgentResultType.Message)
    {
        Console.WriteLine(result.Content);
    }
}
```

### Example 2: Multi-turn Conversation

```csharp
var messages = new List<AgentMessage>
{
    new() { Role = AgentRole.User, Content = "Open Chrome" }
};

await foreach (var result in agent.RunAsync(messages))
{
    if (result.Type == AgentResultType.Message && result.Message != null)
    {
        messages.Add(result.Message);
    }
}

// Continue conversation
messages.Add(new AgentMessage
{
    Role = AgentRole.User,
    Content = "Navigate to google.com"
});

await foreach (var result in agent.RunAsync(messages))
{
    // Process results
}
```

### Example 3: Error Handling

```csharp
try
{
    await foreach (var result in agent.RunAsync(messages, cancellationToken))
    {
        if (result.Type == AgentResultType.Error)
        {
            Console.WriteLine($"Error: {result.Content}");
            break;
        }
        
        // Process results
    }
}
catch (OperationCanceledException)
{
    Console.WriteLine("Agent cancelled");
}
catch (Exception ex)
{
    Console.WriteLine($"Agent failed: {ex.Message}");
}
```

## Best Practices

1. **Screenshot Management**: Keep `OnlyNMostRecentScreenshots` to 3-5 to balance context and cost
2. **Error Handling**: Always handle `AgentResultType.Error` results
3. **Cancellation**: Pass `CancellationToken` for long-running tasks
4. **Message History**: Store conversation history for multi-turn interactions
5. **Resource Cleanup**: Always dispose agent with `await using` or `DisposeAsync()`

## Cost Management

Track token usage through `AgentUsage`:

```csharp
await foreach (var result in agent.RunAsync(messages))
{
    if (result.Usage != null)
    {
        Console.WriteLine($"Tokens: {result.Usage.TotalTokens}");
        Console.WriteLine($"Cost: ${result.Usage.EstimatedCost:F4}");
    }
}
```

## Troubleshooting

### Agent not responding
- Check OpenRouter API key is valid
- Verify computer session is active
- Check network connectivity

### Tool execution failures
- Ensure computer session has proper permissions
- Verify coordinates are within screen bounds
- Check keyboard/mouse commands are valid

### High token usage
- Reduce `OnlyNMostRecentScreenshots`
- Use more specific system prompts
- Break tasks into smaller steps

## License

MIT

