namespace Flekt.Computer.Abstractions;

/// <summary>
/// Mouse operations including clicks, movement, dragging, scrolling, and cursor position.
/// </summary>
public interface IMouse
{
    /// <summary>
    /// Performs a left click at the specified coordinates, or at the current cursor position if not specified.
    /// </summary>
    Task LeftClick(int? x = null, int? y = null, CancellationToken cancelToken = default);
    
    /// <summary>
    /// Performs a right click at the specified coordinates, or at the current cursor position if not specified.
    /// </summary>
    Task RightClick(int? x = null, int? y = null, CancellationToken cancelToken = default);
    
    /// <summary>
    /// Performs a double click at the specified coordinates, or at the current cursor position if not specified.
    /// </summary>
    Task DoubleClick(int? x = null, int? y = null, CancellationToken cancelToken = default);
    
    /// <summary>
    /// Moves the cursor to the specified coordinates.
    /// </summary>
    Task Move(int x, int y, CancellationToken cancelToken = default);
    
    /// <summary>
    /// Presses and holds a mouse button at the specified coordinates.
    /// </summary>
    Task Down(int? x = null, int? y = null, MouseButton button = MouseButton.Left, CancellationToken cancelToken = default);
    
    /// <summary>
    /// Releases a mouse button at the specified coordinates.
    /// </summary>
    Task Up(int? x = null, int? y = null, MouseButton button = MouseButton.Left, CancellationToken cancelToken = default);
    
    /// <summary>
    /// Moves the cursor along a path of points, optionally preserving timing.
    /// </summary>
    Task MovePath(IEnumerable<MousePathPoint> path, MousePathOptions? options = null, CancellationToken cancelToken = default);
    
    /// <summary>
    /// Drags along a path while holding the specified mouse button.
    /// </summary>
    Task Drag(IEnumerable<MousePathPoint> path, MouseButton button = MouseButton.Left, MousePathOptions? options = null, CancellationToken cancelToken = default);
    
    /// <summary>
    /// Simple drag from current position to target coordinates.
    /// </summary>
    Task DragTo(int x, int y, MouseButton button = MouseButton.Left, CancellationToken cancelToken = default);
    
    /// <summary>
    /// Scrolls by the specified amount in pixels.
    /// </summary>
    Task Scroll(int deltaX, int deltaY, CancellationToken cancelToken = default);
    
    /// <summary>
    /// Scrolls down by the specified number of clicks.
    /// </summary>
    Task ScrollDown(int clicks = 1, CancellationToken cancelToken = default);
    
    /// <summary>
    /// Scrolls up by the specified number of clicks.
    /// </summary>
    Task ScrollUp(int clicks = 1, CancellationToken cancelToken = default);
    
    /// <summary>
    /// Gets the current cursor position.
    /// </summary>
    Task<CursorPosition> GetPosition(CancellationToken cancelToken = default);
}

