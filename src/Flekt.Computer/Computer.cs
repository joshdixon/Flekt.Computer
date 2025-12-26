using Flekt.Computer.Abstractions;
using Flekt.Computer.Abstractions.Models;
using Flekt.Computer.Interface;
using Flekt.Computer.Providers;
using Microsoft.Extensions.Logging;

namespace Flekt.Computer;

/// <summary>
/// Main entry point for creating and controlling remote computer instances.
/// Use Computer.CreateAsync() to provision a new computer session.
/// </summary>
public sealed class Computer : IComputer
{
    private readonly IComputerProvider _provider;
    private readonly ComputerOptions _options;
    private readonly IComputerInterface? _interface;

    private Computer(IComputerProvider provider, ComputerOptions options)
    {
        _provider = provider;
        _options = options;
        
        // Create interface if provider supports command sending
        if (provider is ICommandSender commandSender)
        {
            _interface = new ComputerInterface(commandSender);
        }
        
        // Forward state change events from provider
        _provider.StateChanged += (sender, state) => StateChanged?.Invoke(this, state);
    }

    /// <summary>
    /// Creates a new computer instance and waits for it to become ready.
    /// This is the recommended way to create a Computer.
    /// </summary>
    /// <param name="options">Configuration options for the computer.</param>
    /// <param name="loggerFactory">Optional logger factory for diagnostics.</param>
    /// <param name="cancelToken">Cancellation token.</param>
    /// <returns>A ready-to-use Computer instance.</returns>
    public static async Task<Computer> CreateAsync(
        ComputerOptions options,
        ILoggerFactory? loggerFactory = null,
        CancellationToken cancelToken = default)
    {
        // Create provider based on options
        IComputerProvider provider = options.Provider switch
        {
            ProviderType.Cloud => new CloudProvider(loggerFactory?.CreateLogger<CloudProvider>()),
            ProviderType.LocalHyperV => throw new NotImplementedException("LocalHyperV provider not yet implemented"),
            ProviderType.Direct => throw new NotImplementedException("Direct provider not yet implemented"),
            _ => throw new ArgumentException($"Unknown provider type: {options.Provider}")
        };

        var computer = new Computer(provider, options);

        // Connect and wait for ready
        await computer.Run(cancelToken);

        return computer;
    }

    /// <summary>
    /// Gets recording information for a completed session.
    /// </summary>
    /// <param name="sessionId">The session ID to get recording info for.</param>
    /// <param name="options">API connection options.</param>
    /// <param name="cancelToken">Cancellation token.</param>
    /// <returns>Recording info, or null if recording is not available.</returns>
    public static async Task<RecordingInfo?> GetRecordingInfoAsync(
        string sessionId,
        ComputerOptions options,
        CancellationToken cancelToken = default)
    {
        // TODO: Implement API call to get recording info
        // This will call GET /api/sessions/{sessionId}/recording
        throw new NotImplementedException(
            "GetRecordingInfoAsync is not yet implemented. " +
            "This requires the session recording feature to be deployed.");
    }

    /// <summary>
    /// Gets the recorded input events for a completed session.
    /// </summary>
    /// <param name="sessionId">The session ID to get events for.</param>
    /// <param name="options">API connection options.</param>
    /// <param name="cancelToken">Cancellation token.</param>
    /// <returns>List of recorded input events.</returns>
    public static async Task<IReadOnlyList<InputEventData>> GetRecordingEventsAsync(
        string sessionId,
        ComputerOptions options,
        CancellationToken cancelToken = default)
    {
        // TODO: Implement API call to get recording events
        // This will call GET /api/sessions/{sessionId}/recording/events
        throw new NotImplementedException(
            "GetRecordingEventsAsync is not yet implemented. " +
            "This requires the session recording feature to be deployed.");
    }

    // IComputer implementation

    public string? SessionId => _provider.SessionId;
    
    public ComputerState State => _provider.State;
    
    public IComputerInterface Interface => _interface 
        ?? throw new NotSupportedException("This provider does not support the Computer interface.");
    
    public IComputerTracing Tracing => throw new NotImplementedException(
        "Tracing/recording functionality is not yet implemented.");
    
    public event EventHandler<ComputerState>? StateChanged;

    public event EventHandler<InputEventData>? OnInputEvent;

    public async Task Run(CancellationToken cancelToken = default)
    {
        // Connect to provider
        await _provider.ConnectAsync(_options, cancelToken);
        
        // For CloudProvider, wait for the session to become ready
        if (_provider is CloudProvider cloudProvider)
        {
            using var timeoutCts = new CancellationTokenSource(_options.Timeout);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancelToken, timeoutCts.Token);
            
            try
            {
                await cloudProvider.WaitForReadyAsync(linkedCts.Token);
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
            {
                throw new TimeoutException(
                    $"Computer did not become ready within {_options.Timeout.TotalSeconds} seconds. " +
                    $"Current state: {State}");
            }
        }
    }

    public Task<RdpAccessInfo> GetRdpAccess(TimeSpan? duration = null, CancellationToken cancelToken = default)
    {
        return _provider.GetRdpAccessAsync(duration, cancelToken);
    }

    public Task<string> CreateCheckpoint(string? name = null, CancellationToken cancelToken = default)
    {
        throw new NotImplementedException("Checkpoint creation is not yet implemented.");
    }

    public Task RestoreCheckpoint(string checkpointId, CancellationToken cancelToken = default)
    {
        throw new NotImplementedException("Checkpoint restoration is not yet implemented.");
    }

    public Task<EnvironmentInfo> SaveAsEnvironment(SaveEnvironmentOptions options, CancellationToken cancelToken = default)
    {
        throw new NotImplementedException("SaveAsEnvironment is not yet implemented - use SaveAsDiskImage instead.");
    }

    public Task<DiskImageInfo> SaveAsDiskImage(SaveDiskImageOptions options, CancellationToken cancelToken = default)
    {
        return _provider.SaveAsDiskImageAsync(options, cancelToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _provider.DisposeAsync();
    }
}




