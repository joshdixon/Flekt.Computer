namespace Flekt.Computer.Abstractions;

/// <summary>
/// A point in a mouse path with optional timing for replay.
/// </summary>
/// <param name="X">The X coordinate.</param>
/// <param name="Y">The Y coordinate.</param>
/// <param name="DelayFromPrevious">Time since last point. Null means no delay.</param>
public readonly record struct MousePathPoint(
    int X,
    int Y,
    TimeSpan? DelayFromPrevious = null)
{
    /// <summary>
    /// Creates a path point with no delay.
    /// </summary>
    public static MousePathPoint At(int x, int y) => new(x, y);
    
    /// <summary>
    /// Creates a path point with the specified delay from the previous point.
    /// </summary>
    public static MousePathPoint At(int x, int y, TimeSpan delay) => new(x, y, delay);
    
    public CursorPosition ToCursorPosition() => new(X, Y);
    
    public override string ToString() => DelayFromPrevious.HasValue 
        ? $"({X}, {Y}) +{DelayFromPrevious.Value.TotalMilliseconds}ms"
        : $"({X}, {Y})";
}

