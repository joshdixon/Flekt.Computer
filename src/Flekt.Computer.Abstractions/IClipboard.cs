namespace Flekt.Computer.Abstractions;

/// <summary>
/// Clipboard operations.
/// </summary>
public interface IClipboard
{
    // Text (existing)

    /// <summary>
    /// Gets the current clipboard text content.
    /// </summary>
    Task<string> Get(CancellationToken cancelToken = default);

    /// <summary>
    /// Sets the clipboard text content.
    /// </summary>
    Task Set(string text, CancellationToken cancelToken = default);

    // Files

    /// <summary>
    /// Downloads files from URLs and sets them on the clipboard (CF_HDROP).
    /// </summary>
    Task SetFiles(IEnumerable<string> urls, CancellationToken cancelToken = default);

    /// <summary>
    /// Sets files from local paths on the clipboard (CF_HDROP).
    /// </summary>
    Task SetFilesFromPaths(IEnumerable<string> localPaths, CancellationToken cancelToken = default);

    /// <summary>
    /// Gets the clipboard file paths (if clipboard contains files).
    /// </summary>
    Task<string[]?> GetFiles(CancellationToken cancelToken = default);

    // Images

    /// <summary>
    /// Downloads an image from URL and sets it on the clipboard (CF_BITMAP).
    /// </summary>
    Task SetImage(string url, CancellationToken cancelToken = default);

    /// <summary>
    /// Sets an image on the clipboard from raw bytes (CF_BITMAP).
    /// </summary>
    Task SetImage(byte[] imageData, CancellationToken cancelToken = default);

    /// <summary>
    /// Gets the clipboard image as PNG bytes (if clipboard contains an image).
    /// </summary>
    Task<byte[]?> GetImage(CancellationToken cancelToken = default);

    // Content type

    /// <summary>
    /// Gets the current clipboard content type.
    /// </summary>
    Task<ClipboardContentType> GetContentType(CancellationToken cancelToken = default);
}

public enum ClipboardContentType
{
    Empty,
    Text,
    Files,
    Image,
    Other
}

