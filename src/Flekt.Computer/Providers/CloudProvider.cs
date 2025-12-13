using System.Collections.Concurrent;
using Flekt.Computer.Abstractions;
using Flekt.Computer.Abstractions.Contracts;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;

namespace Flekt.Computer.Providers;

/// <summary>
/// Cloud provider that connects to the Computer API via SignalR.
/// Handles session creation, state management, and RDP access through the API.
/// </summary>
internal sealed class CloudProvider : IComputerProvider, IClientHubClient
{
    private readonly ILogger<CloudProvider>? _logger;
    private HubConnection? _connection;
    private string? _sessionId;
    private ComputerState _state = ComputerState.Created;
    private readonly TaskCompletionSource _sessionReadyTcs = new();
    private readonly ConcurrentDictionary<string, TaskCompletionSource<object?>> _pendingRequests = new();
    
    public event EventHandler<ComputerState>? StateChanged;

    public CloudProvider(ILogger<CloudProvider>? logger = null)
    {
        _logger = logger;
    }

    public string? SessionId => _sessionId;
    public ComputerState State => _state;

    public async Task ConnectAsync(ComputerOptions options, CancellationToken cancelToken = default)
    {
        if (options.ApiBaseUrl == null)
        {
            throw new ArgumentException("ApiBaseUrl is required for Cloud provider", nameof(options));
        }

        if (options.ApiKey == null)
        {
            throw new ArgumentException("ApiKey is required for Cloud provider", nameof(options));
        }

        _logger?.LogInformation("Connecting to Computer API at {ApiBaseUrl}", options.ApiBaseUrl);
        
        UpdateState(ComputerState.Connecting);

        // Build SignalR connection
        _connection = new HubConnectionBuilder()
            .WithUrl($"{options.ApiBaseUrl}/hubs/client", httpOptions =>
            {
                httpOptions.Headers.Add("X-API-Key", options.ApiKey);
            })
            .WithAutomaticReconnect(new[] { TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5) })
            .Build();

        // Register callbacks (implement IClientHubClient)
        _connection.On<string>(nameof(SessionReady), SessionReady);
        _connection.On<string, string>(nameof(SessionStateChanged), SessionStateChanged);
        _connection.On<ComputerResponse>(nameof(CommandResponse), CommandResponse);
        _connection.On<string, string, string>(nameof(CheckpointCreated), CheckpointCreated);
        _connection.On<string, string>(nameof(CheckpointRestored), CheckpointRestored);
        _connection.On<string, string, string>(nameof(Error), Error);

        // Connect to hub
        await _connection.StartAsync(cancelToken);
        _logger?.LogInformation("SignalR connection established");

        // Create session
        UpdateState(ComputerState.Provisioning);
        
        var createRequest = new CreateSessionRequest
        {
            EnvironmentId = options.EnvironmentId,
            Vcpu = options.Vcpu,
            MemoryGb = options.MemoryGb,
            StorageGb = options.StorageGb,
            Image = options.Image,
            Tags = options.Tags
        };

        var createResponse = await _connection.InvokeAsync<CreateSessionResponse>(
            "CreateSession",
            createRequest,
            cancelToken);

        _sessionId = createResponse.SessionId;
        _logger?.LogInformation("Session created: {SessionId}", _sessionId);
    }

    public async Task<RdpAccessInfo> GetRdpAccessAsync(TimeSpan? duration = null, CancellationToken cancelToken = default)
    {
        if (_connection == null)
        {
            throw new InvalidOperationException("Not connected. Call ConnectAsync first.");
        }

        if (_sessionId == null)
        {
            throw new InvalidOperationException("No session ID available.");
        }

        if (_state != ComputerState.Ready)
        {
            throw new InvalidOperationException($"Computer is not ready (current state: {_state})");
        }

        _logger?.LogInformation("Requesting RDP access for session {SessionId}", _sessionId);

        var rdpInfo = await _connection.InvokeAsync<RdpAccessInfo>(
            "GetRdpAccess",
            _sessionId,
            duration,
            cancelToken);

        _logger?.LogInformation("RDP access granted, expires at {ExpiresAt}", rdpInfo.ExpiresAt);

        return rdpInfo;
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection != null)
        {
            _logger?.LogInformation("Disposing CloudProvider and ending session {SessionId}", _sessionId);
            
            try
            {
                if (_sessionId != null)
                {
                    await _connection.InvokeAsync("EndSession", _sessionId);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to end session gracefully");
            }
            
            await _connection.DisposeAsync();
            _connection = null;
        }
    }

    // IClientHubClient implementation - callbacks from the API

    public Task SessionReady(string sessionId)
    {
        _logger?.LogInformation("Session ready callback received for {SessionId}", sessionId);
        
        if (sessionId == _sessionId)
        {
            UpdateState(ComputerState.Ready);
            _sessionReadyTcs.TrySetResult();
        }
        
        return Task.CompletedTask;
    }

    public Task SessionStateChanged(string sessionId, string state)
    {
        _logger?.LogInformation("Session state changed: {SessionId} -> {State}", sessionId, state);
        
        if (sessionId == _sessionId && Enum.TryParse<ComputerState>(state, ignoreCase: true, out var newState))
        {
            UpdateState(newState);
        }
        
        return Task.CompletedTask;
    }

    public Task CommandResponse(ComputerResponse response)
    {
        _logger?.LogDebug("Command response received: {CorrelationId}", response.CorrelationId);
        
        if (_pendingRequests.TryRemove(response.CorrelationId, out var tcs))
        {
            if (response.Success)
            {
                tcs.TrySetResult(response.Result);
            }
            else
            {
                tcs.TrySetException(new InvalidOperationException(response.ErrorMessage ?? "Command failed"));
            }
        }
        
        return Task.CompletedTask;
    }

    public Task CheckpointCreated(string sessionId, string checkpointId, string correlationId)
    {
        _logger?.LogInformation("Checkpoint created: {CheckpointId} for session {SessionId}", checkpointId, sessionId);
        
        if (_pendingRequests.TryRemove(correlationId, out var tcs))
        {
            tcs.TrySetResult(checkpointId);
        }
        
        return Task.CompletedTask;
    }

    public Task CheckpointRestored(string sessionId, string correlationId)
    {
        _logger?.LogInformation("Checkpoint restored for session {SessionId}", sessionId);
        
        if (_pendingRequests.TryRemove(correlationId, out var tcs))
        {
            tcs.TrySetResult(null);
        }
        
        return Task.CompletedTask;
    }

    public Task Error(string sessionId, string errorCode, string message)
    {
        _logger?.LogError("Session error: {ErrorCode} - {Message}", errorCode, message);
        
        UpdateState(ComputerState.Error);
        _sessionReadyTcs.TrySetException(new InvalidOperationException($"{errorCode}: {message}"));
        
        return Task.CompletedTask;
    }

    /// <summary>
    /// Waits for the session to become ready (after ConnectAsync).
    /// </summary>
    public Task WaitForReadyAsync(CancellationToken cancelToken = default)
    {
        return _sessionReadyTcs.Task.WaitAsync(cancelToken);
    }

    private void UpdateState(ComputerState newState)
    {
        if (_state != newState)
        {
            var oldState = _state;
            _state = newState;
            _logger?.LogDebug("State changed: {OldState} -> {NewState}", oldState, newState);
            StateChanged?.Invoke(this, newState);
        }
    }
}
