namespace Flekt.Computer.Abstractions;

/// <summary>
/// Interface for controlling a remote computer.
/// Provides access to mouse, keyboard, screen, clipboard, file, shell, and window operations.
/// </summary>
public interface IComputerInterface
{
    /// <summary>
    /// Mouse operations including clicks, movement, dragging, scrolling, and cursor position.
    /// </summary>
    IMouse Mouse { get; }
    
    /// <summary>
    /// Keyboard operations including typing, key presses, and hotkeys.
    /// </summary>
    IKeyboard Keyboard { get; }
    
    /// <summary>
    /// Screen operations including screenshots and display information.
    /// </summary>
    IScreen Screen { get; }
    
    /// <summary>
    /// Clipboard operations.
    /// </summary>
    IClipboard Clipboard { get; }
    
    /// <summary>
    /// File and directory operations.
    /// </summary>
    IFiles Files { get; }
    
    /// <summary>
    /// Shell command execution.
    /// </summary>
    IShell Shell { get; }
    
    /// <summary>
    /// Window management operations.
    /// </summary>
    IWindows Windows { get; }
}
