namespace Flekt.Computer.Abstractions;

/// <summary>
/// Options for path-based mouse operations.
/// </summary>
public sealed class MousePathOptions
{
    /// <summary>
    /// If true, respect the DelayFromPrevious timings in each point.
    /// If false, move as fast as possible.
    /// </summary>
    public bool PreserveTimings { get; init; } = true;
    
    /// <summary>
    /// Override total duration (scales all timings proportionally).
    /// </summary>
    public TimeSpan? TotalDuration { get; init; }
    
    /// <summary>
    /// Speed multiplier (1.0 = normal, 2.0 = double speed, 0.5 = half speed).
    /// </summary>
    public double SpeedMultiplier { get; init; } = 1.0;
    
    /// <summary>
    /// Default options with preserved timings at normal speed.
    /// </summary>
    public static MousePathOptions Default { get; } = new();
    
    /// <summary>
    /// Options for moving as fast as possible without timing delays.
    /// </summary>
    public static MousePathOptions Instant { get; } = new() { PreserveTimings = false };
}

