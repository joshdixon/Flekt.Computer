namespace Flekt.Computer.Abstractions;

/// <summary>
/// Screen operations including screenshots and display information.
/// </summary>
public interface IScreen
{
    /// <summary>
    /// Captures a screenshot of the entire screen.
    /// </summary>
    /// <returns>The screenshot as PNG bytes.</returns>
    Task<byte[]> Screenshot(CancellationToken cancelToken = default);
    
    /// <summary>
    /// Gets the size of the screen.
    /// </summary>
    Task<ScreenSize> GetSize(CancellationToken cancelToken = default);
}

