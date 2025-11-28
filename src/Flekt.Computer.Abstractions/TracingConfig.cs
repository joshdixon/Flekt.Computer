namespace Flekt.Computer.Abstractions;

/// <summary>
/// Configuration for computer session tracing.
/// </summary>
public sealed class TracingConfig
{
    /// <summary>
    /// Whether to capture screenshots on each action.
    /// </summary>
    public bool CaptureScreenshots { get; init; } = true;
    
    /// <summary>
    /// Whether to record API call details.
    /// </summary>
    public bool RecordApiCalls { get; init; } = true;
    
    /// <summary>
    /// Whether to record video of the session.
    /// </summary>
    public bool RecordVideo { get; init; } = false;
    
    /// <summary>
    /// Name for the trace (used in output file naming).
    /// </summary>
    public string? Name { get; init; }
    
    /// <summary>
    /// Custom output path for the trace files.
    /// </summary>
    public string? OutputPath { get; init; }
    
    /// <summary>
    /// Maximum duration for the trace before auto-stopping.
    /// </summary>
    public TimeSpan? MaxDuration { get; init; }
    
    /// <summary>
    /// Default tracing configuration.
    /// </summary>
    public static TracingConfig Default { get; } = new();
    
    /// <summary>
    /// Minimal tracing (API calls only, no screenshots).
    /// </summary>
    public static TracingConfig Minimal { get; } = new() 
    { 
        CaptureScreenshots = false, 
        RecordVideo = false 
    };
    
    /// <summary>
    /// Full tracing including video recording.
    /// </summary>
    public static TracingConfig Full { get; } = new() 
    { 
        CaptureScreenshots = true, 
        RecordApiCalls = true, 
        RecordVideo = true 
    };
}

/// <summary>
/// Options for stopping a trace and saving the output.
/// </summary>
public sealed class TracingStopOptions
{
    /// <summary>
    /// Output path for the trace. If null, uses the config path or default.
    /// </summary>
    public string? OutputPath { get; init; }
    
    /// <summary>
    /// Whether to compress the output into a zip file.
    /// </summary>
    public bool Compress { get; init; } = true;
    
    /// <summary>
    /// Whether to include the video in the output (if recorded).
    /// </summary>
    public bool IncludeVideo { get; init; } = true;
}

