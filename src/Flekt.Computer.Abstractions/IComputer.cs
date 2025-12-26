using Flekt.Computer.Abstractions.Models;

namespace Flekt.Computer.Abstractions;

/// <summary>
/// Represents a remote computer instance that can be controlled.
/// </summary>
public interface IComputer : IAsyncDisposable
{
    /// <summary>
    /// The unique session ID for this computer instance.
    /// </summary>
    string? SessionId { get; }
    
    /// <summary>
    /// The current state of the computer.
    /// </summary>
    ComputerState State { get; }
    
    /// <summary>
    /// The interface for controlling the computer (mouse, keyboard, screen, etc.).
    /// </summary>
    IComputerInterface Interface { get; }
    
    /// <summary>
    /// Session tracing and recording.
    /// </summary>
    IComputerTracing Tracing { get; }
    
    /// <summary>
    /// Event raised when the state changes.
    /// </summary>
    event EventHandler<ComputerState>? StateChanged;

    /// <summary>
    /// Event raised when input events are received from the session recording.
    /// Events are streamed in real-time with ~50ms throttling for mouse moves.
    /// </summary>
    event EventHandler<InputEventData>? OnInputEvent;
    
    /// <summary>
    /// Starts the computer and establishes the connection.
    /// Called automatically by Create(), but can be called manually if using constructor.
    /// </summary>
    Task Run(CancellationToken cancelToken = default);
    
    /// <summary>
    /// Gets RDP access credentials for connecting to the computer.
    /// </summary>
    /// <param name="duration">Optional duration for the credentials.</param>
    /// <param name="cancelToken">Cancellation token.</param>
    Task<RdpAccessInfo> GetRdpAccess(TimeSpan? duration = null, CancellationToken cancelToken = default);
    
    /// <summary>
    /// Creates a checkpoint/snapshot of the current VM state.
    /// </summary>
    /// <param name="name">Optional name for the checkpoint.</param>
    /// <param name="cancelToken">Cancellation token.</param>
    /// <returns>The checkpoint ID.</returns>
    Task<string> CreateCheckpoint(string? name = null, CancellationToken cancelToken = default);
    
    /// <summary>
    /// Restores the VM to a previous checkpoint.
    /// </summary>
    /// <param name="checkpointId">The checkpoint ID to restore.</param>
    /// <param name="cancelToken">Cancellation token.</param>
    Task RestoreCheckpoint(string checkpointId, CancellationToken cancelToken = default);
    
    /// <summary>
    /// Saves the current computer state as a reusable environment.
    /// The environment includes the VM disk image and resource specs.
    /// </summary>
    /// <param name="options">Options for the new environment.</param>
    /// <param name="cancelToken">Cancellation token.</param>
    /// <returns>Information about the created environment.</returns>
    Task<EnvironmentInfo> SaveAsEnvironment(SaveEnvironmentOptions options, CancellationToken cancelToken = default);

    /// <summary>
    /// Captures the current VM disk state as a new reusable image.
    /// 
    /// ⚠️ WARNING: This operation will STOP the VM and END the session.
    /// The VM must be stopped to ensure data consistency when capturing the disk image.
    /// After the capture completes, the session will be terminated and cannot be resumed.
    /// 
    /// The captured image can be used to create new computer instances with the same disk state.
    /// </summary>
    /// <param name="options">Options for the disk image.</param>
    /// <param name="cancelToken">Cancellation token.</param>
    /// <returns>Information about the captured disk image.</returns>
    Task<DiskImageInfo> SaveAsDiskImage(SaveDiskImageOptions options, CancellationToken cancelToken = default);
}

/// <summary>
/// The current state of a computer instance.
/// </summary>
public enum ComputerState
{
    /// <summary>
    /// Initial state, not yet started.
    /// </summary>
    Created,
    
    /// <summary>
    /// Connecting to the provider/agent.
    /// </summary>
    Connecting,
    
    /// <summary>
    /// Waiting for the VM to be provisioned.
    /// </summary>
    Provisioning,
    
    /// <summary>
    /// The computer is ready and the interface is usable.
    /// </summary>
    Ready,
    
    /// <summary>
    /// The connection was lost and is being re-established.
    /// </summary>
    Reconnecting,
    
    /// <summary>
    /// The computer is being shut down.
    /// </summary>
    Stopping,
    
    /// <summary>
    /// The computer has been stopped and disposed.
    /// </summary>
    Stopped,
    
    /// <summary>
    /// An error occurred.
    /// </summary>
    Error
}

