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
byte[] screenshot = await computer.Interface.Screen.Screenshot();

// Click and type
await computer.Interface.Mouse.LeftClick(100, 200);
await computer.Interface.Keyboard.Type("Hello World!");

// Run a command
var result = await computer.Interface.Shell.Run("dir");

// Automatically cleaned up at end of scope
```

### Dependency Injection

```csharp
// In Program.cs or Startup.cs
services.AddFlektComputer(options =>
{
    options.DefaultProvider = ProviderType.Cloud;
    options.ApiBaseUrl = "https://api.computer.flekt.co";
    options.ApiKey = configuration["FlektComputer:ApiKey"];
});

// In your service
public class MyService(IComputerFactory computerFactory, ILogger<MyService> logger)
{
    public async Task DoWork()
    {
        await using var computer = await computerFactory.Create(new ComputerOptions
        {
            EnvironmentId = Guid.Parse("...")
        });

        logger.LogInformation("Taking screenshot...");
        await computer.Interface.Screen.Screenshot();
    }
}
```

### Manual Lifecycle (if needed)

```csharp
var computer = new Computer(new ComputerOptions { ... }, logger);
try
{
    await computer.Run();
    await computer.Interface.Mouse.LeftClick(100, 200);
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

## IComputerInterface API

The interface is organized into domain-specific sub-interfaces:

```csharp
public interface IComputerInterface
{
    IMouse Mouse { get; }
    IKeyboard Keyboard { get; }
    IScreen Screen { get; }
    IClipboard Clipboard { get; }
    IFiles Files { get; }
    IShell Shell { get; }
    IWindows Windows { get; }
}
```

### Mouse

```csharp
await computer.Interface.Mouse.LeftClick(100, 200);
await computer.Interface.Mouse.RightClick();
await computer.Interface.Mouse.DoubleClick(300, 400);
await computer.Interface.Mouse.Move(500, 600);
await computer.Interface.Mouse.Down();
await computer.Interface.Mouse.Up();
await computer.Interface.Mouse.DragTo(700, 800);
await computer.Interface.Mouse.ScrollDown(3);
await computer.Interface.Mouse.ScrollUp();
var position = await computer.Interface.Mouse.GetPosition();
```

### Keyboard

```csharp
await computer.Interface.Keyboard.Type("Hello World!");
await computer.Interface.Keyboard.Press("Enter");
await computer.Interface.Keyboard.Hotkey(default, "Ctrl", "C");
await computer.Interface.Keyboard.Down("Shift");
await computer.Interface.Keyboard.Up("Shift");
```

### Screen

```csharp
byte[] screenshot = await computer.Interface.Screen.Screenshot();
var size = await computer.Interface.Screen.GetSize();
```

### Clipboard

```csharp
string text = await computer.Interface.Clipboard.Get();
await computer.Interface.Clipboard.Set("copied text");
```

### Files

```csharp
bool exists = await computer.Interface.Files.Exists("C:\\file.txt");
string content = await computer.Interface.Files.ReadText("C:\\file.txt");
await computer.Interface.Files.WriteText("C:\\file.txt", "content");
byte[] bytes = await computer.Interface.Files.ReadBytes("C:\\file.bin");
await computer.Interface.Files.WriteBytes("C:\\file.bin", bytes);
await computer.Interface.Files.Delete("C:\\file.txt");
await computer.Interface.Files.CreateDirectory("C:\\folder");
string[] items = await computer.Interface.Files.ListDirectory("C:\\folder");
```

### Shell

```csharp
var result = await computer.Interface.Shell.Run("dir /b");
Console.WriteLine(result.StandardOutput);
Console.WriteLine($"Exit code: {result.ExitCode}");
```

### Windows

```csharp
string activeId = await computer.Interface.Windows.GetActiveId();
var info = await computer.Interface.Windows.GetInfo(activeId);
await computer.Interface.Windows.Activate(windowId);
await computer.Interface.Windows.Close(windowId);
await computer.Interface.Windows.Maximize(windowId);
await computer.Interface.Windows.Minimize(windowId);
var allWindows = await computer.Interface.Windows.List();
```

---

## Mouse Path Operations

For replaying complex recorded mouse movements:

```csharp
// Replay a recorded mouse path with original timings
var recordedPath = new MousePathPoint[]
{
    MousePathPoint.At(100, 100),
    MousePathPoint.At(120, 105, TimeSpan.FromMilliseconds(16)),
    MousePathPoint.At(145, 112, TimeSpan.FromMilliseconds(16)),
    MousePathPoint.At(180, 120, TimeSpan.FromMilliseconds(16)),
};

await computer.Interface.Mouse.MovePath(recordedPath);

// Replay at double speed
await computer.Interface.Mouse.MovePath(recordedPath, new MousePathOptions 
{ 
    SpeedMultiplier = 2.0 
});

// Drag along a path
await computer.Interface.Mouse.Drag(recordedPath, MouseButton.Left);

// Move as fast as possible (no delays)
await computer.Interface.Mouse.MovePath(recordedPath, MousePathOptions.Instant);
```

---

## Environment Workflow

Environments are reusable VM templates. Create a computer, configure it, then save as an environment:

```csharp
// 1. Create a computer from base image
await using var computer = await Computer.Create(new ComputerOptions
{
    Vcpu = 8,
    MemoryGb = 32,
    Image = "windows-11-base",
    Provider = ProviderType.Cloud,
    ApiKey = "your-api-key"
});

// 2. Get RDP access to configure it
var rdp = await computer.GetRdpAccess(TimeSpan.FromHours(4));
Console.WriteLine($"Connect to {rdp.Server} as {rdp.Username}");
// User connects via RDP, installs apps, configures settings...

// 3. Save as reusable environment
var environment = await computer.SaveAsEnvironment(new SaveEnvironmentOptions
{
    Name = "my-app-test-env",
    Description = "Windows 11 with MyApp v2.3 installed"
});

Console.WriteLine($"Environment saved: {environment.Id}");
```

Now create computers from that environment:

```csharp
// Spin up identical computers from the saved environment
await using var testComputer = await Computer.Create(new ComputerOptions
{
    EnvironmentId = environment.Id,  // Uses saved image + specs
    Provider = ProviderType.Cloud,
    ApiKey = "your-api-key"
});

// Starts with exact same state as when saved!
```

---

## Tracing

Record sessions for debugging and analysis:

```csharp
// Start tracing
await computer.Tracing.Start(new TracingConfig
{
    CaptureScreenshots = true,
    RecordApiCalls = true,
    Name = "my-session"
});

// Do some work...
await computer.Interface.Mouse.LeftClick(100, 200);
await computer.Interface.Keyboard.Type("test");

// Stop and save trace
string tracePath = await computer.Tracing.Stop();
```

---

## Project Structure

```
Flekt.Computer.Abstractions/
├── IComputerInterface.cs         # Main interface (composes sub-interfaces)
├── IMouse.cs                     # Mouse operations
├── IKeyboard.cs                  # Keyboard operations
├── IScreen.cs                    # Screen operations
├── IClipboard.cs                 # Clipboard operations
├── IFiles.cs                     # File operations
├── IShell.cs                     # Shell operations
├── IWindows.cs                   # Window operations
├── IComputerTracing.cs           # Tracing interface
├── TracingConfig.cs              # Tracing configuration
└── Models/
    ├── ScreenSize.cs
    ├── CursorPosition.cs
    ├── CommandResult.cs
    ├── MouseButton.cs
    ├── MousePathPoint.cs
    ├── MousePathOptions.cs
    ├── WindowInfo.cs
    └── RdpAccessInfo.cs

Flekt.Computer/
├── Computer.cs                   # Main entry point (IAsyncDisposable)
├── ComputerOptions.cs            # Per-instance configuration
├── ProviderType.cs               # Provider enum
├── ComputerState.cs              # State enum
├── DependencyInjection/
│   ├── IComputerFactory.cs
│   ├── ComputerFactory.cs
│   ├── FlektComputerOptions.cs
│   └── ServiceCollectionExtensions.cs
└── Providers/
    ├── IComputerProvider.cs
    ├── CloudProvider.cs
    ├── LocalHyperVProvider.cs
    └── DirectProvider.cs
```

---

## Dependencies

- `Microsoft.AspNetCore.SignalR.Client` - SignalR client for communication
- `Microsoft.Extensions.DependencyInjection.Abstractions` - DI abstractions
- `Microsoft.Extensions.Options` - Options pattern
- `Microsoft.Extensions.Logging.Abstractions` - Logging abstractions
