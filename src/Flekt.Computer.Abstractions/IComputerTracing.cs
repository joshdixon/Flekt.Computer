namespace Flekt.Computer.Abstractions;

/// <summary>
/// Interface for session tracing and recording.
/// </summary>
public interface IComputerTracing
{
    /// <summary>
    /// Whether tracing is currently active.
    /// </summary>
    bool IsTracing { get; }
    
    /// <summary>
    /// The current tracing configuration, or null if not tracing.
    /// </summary>
    TracingConfig? CurrentConfig { get; }
    
    /// <summary>
    /// Starts tracing the session.
    /// </summary>
    /// <param name="config">Optional configuration for the trace.</param>
    /// <param name="cancelToken">Cancellation token.</param>
    Task Start(TracingConfig? config = null, CancellationToken cancelToken = default);
    
    /// <summary>
    /// Stops tracing and saves the trace output.
    /// </summary>
    /// <param name="options">Options for saving the trace.</param>
    /// <param name="cancelToken">Cancellation token.</param>
    /// <returns>The path to the saved trace.</returns>
    Task<string> Stop(TracingStopOptions? options = null, CancellationToken cancelToken = default);
    
    /// <summary>
    /// Adds custom metadata to the trace.
    /// </summary>
    /// <param name="key">The metadata key.</param>
    /// <param name="value">The metadata value.</param>
    Task AddMetadata(string key, object value);
    
    /// <summary>
    /// Adds a custom event/marker to the trace timeline.
    /// </summary>
    /// <param name="eventName">The name of the event.</param>
    /// <param name="data">Optional data associated with the event.</param>
    Task AddEvent(string eventName, object? data = null);
}
