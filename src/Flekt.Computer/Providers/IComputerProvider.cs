using Flekt.Computer.Abstractions;

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
}
