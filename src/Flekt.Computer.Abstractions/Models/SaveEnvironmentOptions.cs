namespace Flekt.Computer.Abstractions;

/// <summary>
/// Options for saving a computer's state as a reusable environment.
/// </summary>
public sealed class SaveEnvironmentOptions
{
    /// <summary>
    /// Required. Display name for the environment.
    /// </summary>
    public required string Name { get; init; }
    
    /// <summary>
    /// Optional description of what's installed/configured.
    /// </summary>
    public string? Description { get; init; }
    
    /// <summary>
    /// Optional tags/metadata.
    /// </summary>
    public IReadOnlyDictionary<string, string>? Tags { get; init; }
    
    /// <summary>
    /// Whether to shut down the VM cleanly before capturing.
    /// Default: true (recommended for consistent state).
    /// </summary>
    public bool ShutdownBeforeCapture { get; init; } = true;
}

