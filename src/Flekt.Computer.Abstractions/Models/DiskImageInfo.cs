namespace Flekt.Computer.Abstractions;

/// <summary>
/// Information about a captured disk image.
/// </summary>
public sealed class DiskImageInfo
{
    /// <summary>
    /// Unique identifier for the captured image.
    /// </summary>
    public required string ImageId { get; init; }

    /// <summary>
    /// Human-readable name of the image.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Size of the captured image in bytes.
    /// </summary>
    public required long SizeBytes { get; init; }

    /// <summary>
    /// When the image was created.
    /// </summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>
    /// The parent image ID (if this is a differencing disk).
    /// </summary>
    public string? ParentImageId { get; init; }
}

