namespace Flekt.Computer.Abstractions;

/// <summary>
/// Information about a saved environment.
/// </summary>
public sealed record EnvironmentInfo
{
    /// <summary>
    /// The unique identifier for this environment.
    /// </summary>
    public required Guid Id { get; init; }
    
    /// <summary>
    /// Display name for the environment.
    /// </summary>
    public required string Name { get; init; }
    
    /// <summary>
    /// Description of what's installed/configured.
    /// </summary>
    public string? Description { get; init; }
    
    /// <summary>
    /// Number of virtual CPUs.
    /// </summary>
    public required int Vcpu { get; init; }
    
    /// <summary>
    /// Memory in gigabytes.
    /// </summary>
    public required int MemoryGb { get; init; }
    
    /// <summary>
    /// Storage in gigabytes.
    /// </summary>
    public required int StorageGb { get; init; }
    
    /// <summary>
    /// When the environment was created.
    /// </summary>
    public required DateTimeOffset CreatedAt { get; init; }
    
    /// <summary>
    /// Tags/metadata associated with the environment.
    /// </summary>
    public IReadOnlyDictionary<string, string>? Tags { get; init; }
}

