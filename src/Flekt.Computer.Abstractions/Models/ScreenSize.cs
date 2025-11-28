namespace Flekt.Computer.Abstractions;

/// <summary>
/// Represents the dimensions of a screen or window.
/// </summary>
public readonly record struct ScreenSize(int Width, int Height)
{
    public static ScreenSize Empty => new(0, 0);
    
    public int Area => Width * Height;
    
    public double AspectRatio => Height == 0 ? 0 : (double)Width / Height;
    
    public override string ToString() => $"{Width}x{Height}";
}

