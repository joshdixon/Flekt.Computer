namespace Flekt.Computer.Abstractions;

/// <summary>
/// Request to create a new computer session (SDK → API).
/// </summary>
public sealed record CreateSessionRequest
{
    /// <summary>
    /// Optional environment ID to use (includes image + specs).
    /// </summary>
    public Guid? EnvironmentId { get; init; }

    /// <summary>
    /// Number of virtual CPUs (if not using environment).
    /// </summary>
    public int? Vcpu { get; init; }

    /// <summary>
    /// Memory in GB (if not using environment).
    /// </summary>
    public int? MemoryGb { get; init; }

    /// <summary>
    /// Storage in GB (if not using environment).
    /// </summary>
    public int? StorageGb { get; init; }

    /// <summary>
    /// The ID of a disk image to create the VM from.
    /// Use this to create VMs from images captured with SaveAsDiskImage().
    /// If null, uses the default base image.
    /// </summary>
    public string? ImageId { get; init; }

    /// <summary>
    /// Optional tags/metadata.
    /// </summary>
    public IReadOnlyDictionary<string, string>? Tags { get; init; }
}

/// <summary>
/// Response after creating a session.
/// </summary>
public sealed record CreateSessionResponse
{
    /// <summary>
    /// The created session ID.
    /// </summary>
    public required string SessionId { get; init; }

    /// <summary>
    /// Initial session status.
    /// </summary>
    public required string Status { get; init; }
}





