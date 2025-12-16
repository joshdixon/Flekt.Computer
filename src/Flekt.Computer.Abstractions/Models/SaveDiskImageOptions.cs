namespace Flekt.Computer.Abstractions;

/// <summary>
/// Options for capturing a computer's disk state as a reusable image.
/// 
/// ⚠️ WARNING: This operation will STOP the VM and END the session.
/// The VM must be stopped to ensure data consistency when capturing the disk image.
/// After the capture completes, the session will be terminated and cannot be resumed.
/// </summary>
public sealed class SaveDiskImageOptions
{
    /// <summary>
    /// Required. Display name for the disk image.
    /// </summary>
    public required string Name { get; init; }
    
    /// <summary>
    /// Optional description of what's installed/configured in this image.
    /// </summary>
    public string? Description { get; init; }
    
    /// <summary>
    /// Optional tags/metadata for categorization.
    /// </summary>
    public IReadOnlyDictionary<string, string>? Tags { get; init; }
}

