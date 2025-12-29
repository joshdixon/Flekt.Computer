namespace Flekt.Computer.Abstractions.Models;

/// <summary>
/// Information about a computer session.
/// </summary>
public record SessionInfo
{
    /// <summary>
    /// The unique session ID.
    /// </summary>
    public required string SessionId { get; init; }

    /// <summary>
    /// Current state of the session.
    /// </summary>
    public required string State { get; init; }

    /// <summary>
    /// WebRTC URL for browser-based screen streaming.
    /// Only available after the stream is ready.
    /// </summary>
    public string? WebRtcUrl { get; init; }

    /// <summary>
    /// When the session was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>
    /// When the session became ready (VM started, agent connected).
    /// </summary>
    public DateTimeOffset? ReadyAt { get; init; }
}
