namespace Flekt.Computer.Abstractions.Models;

/// <summary>
/// Information about a session recording.
/// Available after a session ends and recording is finalized.
/// </summary>
public record RecordingInfo
{
    /// <summary>
    /// The session ID this recording belongs to.
    /// </summary>
    public required string SessionId { get; init; }

    /// <summary>
    /// When recording started.
    /// </summary>
    public required DateTimeOffset StartedAt { get; init; }

    /// <summary>
    /// When recording ended.
    /// </summary>
    public required DateTimeOffset EndedAt { get; init; }

    /// <summary>
    /// Total duration of the recording.
    /// </summary>
    public required TimeSpan Duration { get; init; }

    /// <summary>
    /// URL to download/stream the video recording (MP4).
    /// This is a presigned URL with limited validity.
    /// </summary>
    public required string VideoUrl { get; init; }

    /// <summary>
    /// URL to download the input events file (JSONL format).
    /// This is a presigned URL with limited validity.
    /// </summary>
    public required string EventsUrl { get; init; }

    /// <summary>
    /// Total number of input events recorded.
    /// </summary>
    public required int TotalEvents { get; init; }

    /// <summary>
    /// Video frame rate (frames per second).
    /// </summary>
    public int VideoFps { get; init; } = 25;

    /// <summary>
    /// Video width in pixels.
    /// </summary>
    public int VideoWidth { get; init; }

    /// <summary>
    /// Video height in pixels.
    /// </summary>
    public int VideoHeight { get; init; }
}
