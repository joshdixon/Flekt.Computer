using Flekt.Computer.Abstractions;
using Flekt.Computer.Abstractions.Models;

namespace Flekt.Computer.Providers;

/// <summary>
/// Abstraction for different connection mechanisms to remote computers.
/// Providers handle the underlying transport (SignalR, gRPC, etc.) and
/// session lifecycle management.
/// </summary>
public interface IComputerProvider : IAsyncDisposable
{
    /// <summary>
    /// The session ID assigned by the provider, or null if not yet connected.
    /// </summary>
    string? SessionId { get; }
    
    /// <summary>
    /// The current state of the computer session.
    /// </summary>
    ComputerState State { get; }
    
    /// <summary>
    /// Event raised when the computer state changes.
    /// </summary>
    event EventHandler<ComputerState>? StateChanged;

    /// <summary>
    /// Event raised when an input event is received from the remote computer.
    /// Used for real-time session recording/streaming.
    /// </summary>
    event EventHandler<InputEventData>? OnInputEvent;

    /// <summary>
    /// Event raised when an RDP connection event is received (connect/disconnect).
    /// Used to detect when users connect/disconnect via RDP.
    /// </summary>
    event EventHandler<RdpConnectionEvent>? OnRdpConnectionChanged;

    /// <summary>
    /// Connects to the remote computer and establishes a session.
    /// Returns when the connection is established (not necessarily when the computer is Ready).
    /// </summary>
    /// <param name="options">Connection options including provider-specific configuration.</param>
    /// <param name="cancelToken">Cancellation token.</param>
    Task ConnectAsync(ComputerOptions options, CancellationToken cancelToken = default);
    
    /// <summary>
    /// Gets RDP access credentials and connection information.
    /// </summary>
    /// <param name="duration">How long the credentials should remain valid (provider-specific default if null).</param>
    /// <param name="cancelToken">Cancellation token.</param>
    /// <returns>RDP connection information including credentials.</returns>
    Task<RdpAccessInfo> GetRdpAccessAsync(TimeSpan? duration = null, CancellationToken cancelToken = default);

    /// <summary>
    /// Gets the WebRTC URL for browser-based screen streaming.
    /// Returns null if the stream is not yet ready.
    /// </summary>
    /// <param name="cancelToken">Cancellation token.</param>
    Task<string?> GetWebRtcUrlAsync(CancellationToken cancelToken = default);

    /// <summary>
    /// Captures the current VM disk state as a new reusable image.
    /// ⚠️ WARNING: This operation will STOP the VM and END the session.
    /// </summary>
    /// <param name="options">Options for the disk image.</param>
    /// <param name="cancelToken">Cancellation token.</param>
    /// <returns>Information about the captured disk image.</returns>
    Task<DiskImageInfo> SaveAsDiskImageAsync(SaveDiskImageOptions options, CancellationToken cancelToken = default);
}





