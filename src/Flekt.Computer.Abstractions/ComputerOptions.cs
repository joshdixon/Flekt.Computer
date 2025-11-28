namespace Flekt.Computer.Abstractions;

/// <summary>
/// Options for creating a computer instance.
/// </summary>
public sealed class ComputerOptions
{
    /// <summary>
    /// The ID of a premade environment to use (includes image + specs).
    /// Mutually exclusive with specifying Vcpu/MemoryGb/StorageGb/Image directly.
    /// </summary>
    public Guid? EnvironmentId { get; init; }
    
    /// <summary>
    /// Number of virtual CPUs (4, 8, 16, 32).
    /// </summary>
    public int? Vcpu { get; init; }
    
    /// <summary>
    /// Memory in gigabytes (16, 32, 64, 128).
    /// </summary>
    public int? MemoryGb { get; init; }
    
    /// <summary>
    /// Storage in gigabytes (128, 256, 512, 1024).
    /// </summary>
    public int? StorageGb { get; init; }
    
    /// <summary>
    /// Base image name (e.g., "windows-11-base").
    /// </summary>
    public string? Image { get; init; }
    
    /// <summary>
    /// The provider type to use for connecting.
    /// </summary>
    public ProviderType Provider { get; init; } = ProviderType.Cloud;
    
    /// <summary>
    /// API key for authentication (required for Cloud provider).
    /// </summary>
    public string? ApiKey { get; init; }
    
    /// <summary>
    /// Base URL for the API (Cloud provider).
    /// </summary>
    public string? ApiBaseUrl { get; init; }
    
    /// <summary>
    /// URL for the local Host (LocalHyperV provider).
    /// </summary>
    public string? LocalHostUrl { get; init; }
    
    /// <summary>
    /// URL for the Agent (Direct provider).
    /// </summary>
    public string? AgentUrl { get; init; }
    
    /// <summary>
    /// Display name for the computer/VM.
    /// </summary>
    public string? Name { get; init; }
    
    /// <summary>
    /// Timeout for the computer to become ready.
    /// </summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromMinutes(5);
    
    /// <summary>
    /// Tags/metadata to associate with the computer.
    /// </summary>
    public IReadOnlyDictionary<string, string>? Tags { get; init; }
}

/// <summary>
/// The type of provider to use for connecting to remote computers.
/// </summary>
public enum ProviderType
{
    /// <summary>
    /// Connect through the cloud API (Api → Host → Agent).
    /// Use this for production deployments.
    /// </summary>
    Cloud,
    
    /// <summary>
    /// Connect directly to a local HyperV Host.
    /// Use this for local development without the cloud API.
    /// </summary>
    LocalHyperV,
    
    /// <summary>
    /// Connect directly to an already-running Agent.
    /// Use this for testing and debugging.
    /// </summary>
    Direct
}

