namespace Flekt.Computer.Abstractions.Models;

/// <summary>
/// Types of input events captured during session recording.
/// </summary>
public enum InputEventType
{
    /// <summary>
    /// Mouse cursor moved.
    /// </summary>
    MouseMove,

    /// <summary>
    /// Mouse button pressed down.
    /// </summary>
    MouseDown,

    /// <summary>
    /// Mouse button released.
    /// </summary>
    MouseUp,

    /// <summary>
    /// Keyboard key pressed down.
    /// </summary>
    KeyDown,

    /// <summary>
    /// Keyboard key released.
    /// </summary>
    KeyUp,

    /// <summary>
    /// Text was placed on the clipboard.
    /// </summary>
    ClipboardText,

    /// <summary>
    /// Files were placed on the clipboard.
    /// </summary>
    ClipboardFile,

    /// <summary>
    /// An image was placed on the clipboard.
    /// </summary>
    ClipboardImage
}

/// <summary>
/// Represents an input event captured during session recording.
/// Events are streamed in real-time and also stored for playback.
/// </summary>
/// <param name="Timestamp">When the event occurred.</param>
/// <param name="SessionId">The session this event belongs to.</param>
/// <param name="EventType">The type of input event.</param>
/// <param name="X">Mouse X coordinate (for mouse events).</param>
/// <param name="Y">Mouse Y coordinate (for mouse events).</param>
/// <param name="MouseButton">Which mouse button (for click events).</param>
/// <param name="KeyCode">Virtual key code (for keyboard events).</param>
/// <param name="KeyName">Key name (for keyboard events).</param>
/// <param name="ClipboardText">Clipboard text content (for ClipboardText events).</param>
/// <param name="ClipboardFileBlobUrls">Blob URLs for clipboard files (for ClipboardFile events).</param>
/// <param name="ClipboardImageBlobUrl">Blob URL for clipboard image (for ClipboardImage events).</param>
public record InputEventData(
    DateTimeOffset Timestamp,
    string SessionId,
    InputEventType EventType,
    int? X = null,
    int? Y = null,
    MouseButton? MouseButton = null,
    int? KeyCode = null,
    string? KeyName = null,
    string? ClipboardText = null,
    string[]? ClipboardFileBlobUrls = null,
    string? ClipboardImageBlobUrl = null
);
