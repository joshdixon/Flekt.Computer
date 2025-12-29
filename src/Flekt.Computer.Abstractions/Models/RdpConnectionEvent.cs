namespace Flekt.Computer.Abstractions.Models;

/// <summary>
/// Types of RDP connection events.
/// </summary>
public enum RdpConnectionType
{
    /// <summary>
    /// A user connected via RDP.
    /// </summary>
    Connected,

    /// <summary>
    /// A user disconnected from RDP.
    /// </summary>
    Disconnected
}

/// <summary>
/// Represents an RDP connection event on the VM.
/// These events are used to detect when a user connects/disconnects via RDP,
/// which is useful for automatically starting/stopping recording.
/// </summary>
/// <param name="SessionId">The session this event belongs to.</param>
/// <param name="Type">Whether this is a connect or disconnect event.</param>
/// <param name="Timestamp">When the event occurred.</param>
/// <param name="ClientIpAddress">The IP address of the connecting RDP client (if available).</param>
/// <param name="ClientHostname">The hostname of the connecting RDP client (if available).</param>
/// <param name="WindowsSessionId">The Windows session ID for the RDP session.</param>
public record RdpConnectionEvent(
    string SessionId,
    RdpConnectionType Type,
    DateTimeOffset Timestamp,
    string? ClientIpAddress = null,
    string? ClientHostname = null,
    int? WindowsSessionId = null
);
