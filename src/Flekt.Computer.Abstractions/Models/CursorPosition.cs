namespace Flekt.Computer.Abstractions;

/// <summary>
/// Represents the current position of the mouse cursor.
/// </summary>
public readonly record struct CursorPosition(int X, int Y)
{
    public static CursorPosition Origin => new(0, 0);
    
    /// <summary>
    /// Calculates the distance to another cursor position.
    /// </summary>
    public double DistanceTo(CursorPosition other)
    {
        int dx = other.X - X;
        int dy = other.Y - Y;
        return Math.Sqrt((dx * dx) + (dy * dy));
    }
    
    public override string ToString() => $"({X}, {Y})";
}

