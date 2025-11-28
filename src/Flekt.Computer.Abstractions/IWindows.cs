namespace Flekt.Computer.Abstractions;

/// <summary>
/// Window management operations.
/// </summary>
public interface IWindows
{
    /// <summary>
    /// Gets the ID of the currently active window.
    /// </summary>
    Task<string> GetActiveId(CancellationToken cancelToken = default);
    
    /// <summary>
    /// Gets information about a window.
    /// </summary>
    Task<WindowInfo> GetInfo(string windowId, CancellationToken cancelToken = default);
    
    /// <summary>
    /// Activates (brings to foreground) a window.
    /// </summary>
    Task Activate(string windowId, CancellationToken cancelToken = default);
    
    /// <summary>
    /// Closes a window.
    /// </summary>
    Task Close(string windowId, CancellationToken cancelToken = default);
    
    /// <summary>
    /// Maximizes a window.
    /// </summary>
    Task Maximize(string windowId, CancellationToken cancelToken = default);
    
    /// <summary>
    /// Minimizes a window.
    /// </summary>
    Task Minimize(string windowId, CancellationToken cancelToken = default);
    
    /// <summary>
    /// Restores a window to its normal state.
    /// </summary>
    Task Restore(string windowId, CancellationToken cancelToken = default);
    
    /// <summary>
    /// Lists all visible windows.
    /// </summary>
    Task<IReadOnlyList<WindowInfo>> List(CancellationToken cancelToken = default);
}

