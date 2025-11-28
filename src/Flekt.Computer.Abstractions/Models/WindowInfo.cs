namespace Flekt.Computer.Abstractions;

/// <summary>
/// Information about a window on the remote computer.
/// </summary>
public sealed record WindowInfo
{
    /// <summary>
    /// The unique identifier for the window (typically the handle as a string).
    /// </summary>
    public required string Id { get; init; }
    
    /// <summary>
    /// The title/name of the window.
    /// </summary>
    public required string Title { get; init; }
    
    /// <summary>
    /// The process name that owns the window.
    /// </summary>
    public string? ProcessName { get; init; }
    
    /// <summary>
    /// The process ID that owns the window.
    /// </summary>
    public int? ProcessId { get; init; }
    
    /// <summary>
    /// The size of the window.
    /// </summary>
    public ScreenSize? Size { get; init; }
    
    /// <summary>
    /// The position of the window (top-left corner).
    /// </summary>
    public CursorPosition? Position { get; init; }
    
    /// <summary>
    /// Whether the window is currently visible.
    /// </summary>
    public bool IsVisible { get; init; }
    
    /// <summary>
    /// Whether the window is minimized.
    /// </summary>
    public bool IsMinimized { get; init; }
    
    /// <summary>
    /// Whether the window is maximized.
    /// </summary>
    public bool IsMaximized { get; init; }
}

