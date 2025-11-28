namespace Flekt.Computer.Abstractions;

/// <summary>
/// Clipboard operations.
/// </summary>
public interface IClipboard
{
    /// <summary>
    /// Gets the current clipboard text content.
    /// </summary>
    Task<string> Get(CancellationToken cancelToken = default);
    
    /// <summary>
    /// Sets the clipboard text content.
    /// </summary>
    Task Set(string text, CancellationToken cancelToken = default);
}

