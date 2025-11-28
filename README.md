# Flekt.Computer

A C# SDK for controlling remote computer instances, inspired by [CUA's Computer SDK](https://github.com/trycua/cua).

## Overview

Flekt.Computer provides a simple, async API for interacting with remote Windows computers running Flekt.Computer.Agent. It supports multiple providers for different deployment scenarios.

## Installation

```bash
dotnet add package Flekt.Computer
```

## Quick Start

### Static Factory (Simple Usage)

```csharp
using Flekt.Computer;

// Auto cleanup with await using
await using var computer = await Computer.Create(new ComputerOptions
{
    EnvironmentId = Guid.Parse("..."),
    Provider = ProviderType.Cloud,
    ApiKey = "your-api-key"
});

// Take a screenshot
byte[] screenshot = await computer.Interface.Screenshot();

// Click and type
await computer.Interface.LeftClick(100, 200);
await computer.Interface.TypeText("Hello World!");

// Automatically cleaned up at end of scope
```

### Dependency Injection

```csharp
// In Program.cs or Startup.cs
services.AddFlektComputer(options =>
{
    options.DefaultProvider = ProviderType.Cloud;
    options.ApiBaseUrl = "https://api.flekt.computer";
    options.ApiKey = configuration["FlektComputer:ApiKey"];
});

// In your service
public class MyService
{
    private readonly IComputerFactory _computerFactory;
    private readonly ILogger<MyService> _logger;

    public MyService(IComputerFactory computerFactory, ILogger<MyService> logger)
    {
        _computerFactory = computerFactory;
        _logger = logger;
    }

    public async Task DoWork()
    {
        await using var computer = await _computerFactory.Create(new ComputerOptions
        {
            EnvironmentId = Guid.Parse("...")
        });

        _logger.LogInformation("Taking screenshot...");
        await computer.Interface.Screenshot();
    }
}
```

### Manual Lifecycle (if needed)

```csharp
var computer = new Computer(new ComputerOptions { ... }, logger);
try
{
    await computer.Run();
    await computer.Interface.LeftClick(100, 200);
}
finally
{
    await computer.DisposeAsync();
}
```

## Providers

| Provider | Use Case | Connects To |
|----------|----------|-------------|
| `Cloud` | Production | Computer.Api → Host → Agent |
| `LocalHyperV` | Local development | Host → Agent |
| `Direct` | Testing | Agent directly |

---

## Project Structure

```
Flekt.Computer/
├── Computer.cs                    # Main entry point (IAsyncDisposable)
├── ComputerOptions.cs             # Per-instance configuration
├── IComputerInterface.cs          # Control interface
├── ComputerTracing.cs             # Session recording
├── TracingWrapper.cs              # Auto-trace decorator
├── DependencyInjection/
│   ├── IComputerFactory.cs        # Factory for DI scenarios
│   ├── ComputerFactory.cs         # Default factory implementation
│   ├── FlektComputerOptions.cs    # Global DI configuration
│   └── ServiceCollectionExtensions.cs  # AddFlektComputer() extension
├── Providers/
│   ├── IComputerProvider.cs       # Provider abstraction
│   ├── CloudProvider.cs           # Cloud (Api → Host → Agent)
│   ├── LocalHyperVProvider.cs     # Local HyperV (Host → Agent)
│   └── DirectProvider.cs          # Direct to Agent
├── Models/
│   ├── ScreenSize.cs
│   ├── CursorPosition.cs
│   ├── CommandResult.cs
│   ├── MouseButton.cs
│   ├── MousePathPoint.cs
│   └── MousePathOptions.cs
└── Flekt.Computer.csproj
```

---

## Implementation Plan

### Phase 1: Core Interfaces & Models

- [ ] Create `Flekt.Computer.csproj`
- [ ] Define `IComputerInterface` with all methods
- [ ] Create model classes (`ScreenSize`, `CursorPosition`, `CommandResult`, `MouseButton`)
- [ ] Define `ComputerOptions` with Environment/specs support
- [ ] Define `ProviderType` enum

### Phase 2: Provider Abstraction

- [ ] Define `IComputerProvider` interface
- [ ] Implement provider factory
- [ ] Stub out `CloudProvider`, `LocalHyperVProvider`, `DirectProvider`

### Phase 3: Computer Class

- [ ] Implement `Computer` class (main entry point, implements `IAsyncDisposable`)
- [ ] Add constructor accepting `ILogger<Computer>?` (optional)
- [ ] Implement static `Computer.Create()` factory for inline `await using`
- [ ] Implement `Run()` for manual lifecycle
- [ ] Implement `DisposeAsync()` for cleanup (stops VM, closes connections)
- [ ] Implement `Interface` property (returns `IComputerInterface`)

### Phase 3b: Dependency Injection

- [ ] Define `FlektComputerOptions` for global configuration
- [ ] Define `IComputerFactory` interface
- [ ] Implement `ComputerFactory` (injects ILogger, IOptions)
- [ ] Implement `AddFlektComputer()` extension method for IServiceCollection

### Phase 4: DirectProvider (First Working Provider)

- [ ] Implement SignalR connection to Agent
- [ ] Implement all `IComputerInterface` methods via SignalR
- [ ] Add correlation ID tracking for request/response
- [ ] Add connection state management and reconnection

### Phase 5: Tracing

- [ ] Implement `ComputerTracing` class
- [ ] Implement `TracingWrapper` (decorator for IComputerInterface)
- [ ] Add trace events for all API calls
- [ ] Add screenshot capture on actions
- [ ] Implement trace export (zip/directory)

### Phase 6: LocalHyperVProvider

- [ ] Connect to local Host via SignalR
- [ ] Request VM creation with Environment/specs
- [ ] Wait for Agent connection
- [ ] Route commands through Host

### Phase 7: CloudProvider

- [ ] Connect to Computer.Api via SignalR
- [ ] Authenticate with API key
- [ ] Request computer session
- [ ] Handle session establishment flow
- [ ] Route commands through Api → Host → Agent

---

## IComputerInterface API

```csharp
public interface IComputerInterface
{
    // Mouse - Simple
    Task LeftClick(int? x = null, int? y = null);
    Task RightClick(int? x = null, int? y = null);
    Task DoubleClick(int? x = null, int? y = null);
    Task MoveCursor(int x, int y);
    Task MouseDown(int? x = null, int? y = null, MouseButton button = MouseButton.Left);
    Task MouseUp(int? x = null, int? y = null, MouseButton button = MouseButton.Left);
    
    // Mouse - Path-based (complex movements)
    Task MoveCursorPath(IEnumerable<MousePathPoint> path, MousePathOptions? options = null);
    Task Drag(IEnumerable<MousePathPoint> path, MouseButton button = MouseButton.Left, MousePathOptions? options = null);
    Task DragTo(int x, int y, MouseButton button = MouseButton.Left);  // Simple drag (current pos → target)
    
    // Keyboard
    Task TypeText(string text);
    Task PressKey(string key);
    Task Hotkey(params string[] keys);
    Task KeyDown(string key);
    Task KeyUp(string key);
    
    // Scroll
    Task Scroll(int x, int y);
    Task ScrollDown(int clicks = 1);
    Task ScrollUp(int clicks = 1);
    
    // Screen
    Task<byte[]> Screenshot();
    Task<ScreenSize> GetScreenSize();
    Task<CursorPosition> GetCursorPosition();
    
    // Clipboard
    Task<string> GetClipboard();
    Task SetClipboard(string text);
    
    // Files
    Task<bool> FileExists(string path);
    Task<string> ReadText(string path);
    Task WriteText(string path, string content);
    Task<byte[]> ReadBytes(string path, int offset = 0, int? length = null);
    Task WriteBytes(string path, byte[] content);
    Task DeleteFile(string path);
    Task<bool> DirectoryExists(string path);
    Task CreateDirectory(string path);
    Task DeleteDirectory(string path);
    Task<string[]> ListDirectory(string path);
    
    // Shell
    Task<CommandResult> RunCommand(string command);
    
    // Window Management
    Task<string> GetCurrentWindowId();
    Task<string> GetWindowName(string windowId);
    Task<ScreenSize> GetWindowSize(string windowId);
    Task ActivateWindow(string windowId);
    Task CloseWindow(string windowId);
    Task MaximizeWindow(string windowId);
    Task MinimizeWindow(string windowId);
}
```

## ComputerTracing API

```csharp
public class ComputerTracing
{
    bool IsTracing { get; }
    
    Task Start(TracingConfig? config = null);
    Task<string> Stop(StopOptions? options = null);
    Task AddMetadata(string key, object value);
}

public class TracingConfig
{
    public bool Screenshots { get; set; } = true;
    public bool ApiCalls { get; set; } = true;
    public bool Video { get; set; } = false;
    public string? Name { get; set; }
    public string? Path { get; set; }
}

public class StopOptions
{
    public string? OutputPath { get; set; }  // Directory to save trace (default: ./traces/{name})
}
```

## Dependency Injection API

```csharp
// Global configuration (set once at startup)
public class FlektComputerOptions
{
    public ProviderType DefaultProvider { get; set; } = ProviderType.Cloud;
    public string? ApiBaseUrl { get; set; }
    public string? ApiKey { get; set; }
    public string? LocalHostUrl { get; set; }  // For LocalHyperV provider
}

// Factory interface for creating computers
public interface IComputerFactory
{
    Task<Computer> Create(ComputerOptions options);
    Task<Computer> Create(Guid environmentId);  // Convenience overload
}

// Extension method for registration
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddFlektComputer(
        this IServiceCollection services,
        Action<FlektComputerOptions>? configure = null);
}
```

## Mouse Path Models

For replaying complex recorded mouse movements:

```csharp
/// <summary>
/// A point in a mouse path with optional timing.
/// </summary>
public record MousePathPoint(
    int X,
    int Y,
    TimeSpan? DelayFromPrevious = null  // Time since last point (null = no delay)
);

/// <summary>
/// Options for path-based mouse operations.
/// </summary>
public class MousePathOptions
{
    /// <summary>
    /// If true, respect the DelayFromPrevious timings in each point.
    /// If false, move as fast as possible.
    /// </summary>
    public bool PreserveTimings { get; set; } = true;
    
    /// <summary>
    /// Override total duration (scales all timings proportionally).
    /// </summary>
    public TimeSpan? TotalDuration { get; set; }
    
    /// <summary>
    /// Speed multiplier (1.0 = normal, 2.0 = double speed, 0.5 = half speed).
    /// </summary>
    public double SpeedMultiplier { get; set; } = 1.0;
}
```

### Usage Examples

```csharp
// Replay a recorded mouse path with original timings
var recordedPath = new[]
{
    new MousePathPoint(100, 100, TimeSpan.Zero),
    new MousePathPoint(120, 105, TimeSpan.FromMilliseconds(16)),
    new MousePathPoint(145, 112, TimeSpan.FromMilliseconds(16)),
    new MousePathPoint(180, 120, TimeSpan.FromMilliseconds(16)),
    new MousePathPoint(220, 125, TimeSpan.FromMilliseconds(16)),
};

await computer.Interface.MoveCursorPath(recordedPath);

// Replay at double speed
await computer.Interface.MoveCursorPath(recordedPath, new MousePathOptions 
{ 
    SpeedMultiplier = 2.0 
});

// Drag along a path (e.g., drawing, selecting)
await computer.Interface.Drag(recordedPath, MouseButton.Left);

// Move as fast as possible (no delays)
await computer.Interface.MoveCursorPath(recordedPath, new MousePathOptions 
{ 
    PreserveTimings = false 
});
```

### Converting from Flekt's InputEventData

```csharp
// Helper to convert recorded inputs to path
public static IEnumerable<MousePathPoint> ToMousePath(
    IEnumerable<InputEventData> inputs, 
    DateTimeOffset recordingStartedAt)
{
    DateTimeOffset? previousTime = null;
    
    foreach (var input in inputs.Where(i => i.EventType == InputEventType.MouseMove))
    {
        var delay = previousTime.HasValue 
            ? input.Timestamp - previousTime.Value 
            : TimeSpan.Zero;
            
        yield return new MousePathPoint(input.X!.Value, input.Y!.Value, delay);
        previousTime = input.Timestamp;
    }
}
```

---

## Dependencies

- `Microsoft.AspNetCore.SignalR.Client` - SignalR client for communication
- `Microsoft.Extensions.DependencyInjection.Abstractions` - DI abstractions
- `Microsoft.Extensions.Options` - Options pattern
- `Microsoft.Extensions.Logging.Abstractions` - Logging abstractions
- `System.Text.Json` - JSON serialization

