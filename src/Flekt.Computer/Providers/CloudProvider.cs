using System.Collections.Concurrent;
using System.Text.Json;
using Flekt.Computer.Abstractions;
using Flekt.Computer.Abstractions.Contracts;
using Flekt.Computer.Interface;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;

using TypedSignalR.Client;

namespace Flekt.Computer.Providers;

/// <summary>
/// Cloud provider that connects to the Computer API via SignalR.
/// Handles session creation, state management, and RDP access through the API.
/// Uses TypedSignalR.Client for compile-time type safety.
/// </summary>
internal sealed class CloudProvider : IComputerProvider, IClientHubClient, ICommandSender, IAsyncDisposable
{
    private const int MaxRetries = 3;
    private static readonly TimeSpan ReconnectWaitTimeout = TimeSpan.FromSeconds(30);
    
    private readonly ILogger<CloudProvider>? _logger;
    private HubConnection? _connection;
    private IClientHubServer? _hubProxy;
    private IDisposable? _receiverSubscription;
    private string? _sessionId;
    private ComputerState _state = ComputerState.Created;
    private readonly TaskCompletionSource _sessionReadyTcs = new();
    private readonly ConcurrentDictionary<string, TaskCompletionSource<object?>> _pendingRequests = new();
    private TaskCompletionSource? _reconnectedTcs;
    
    public event EventHandler<ComputerState>? StateChanged;

    public CloudProvider(ILogger<CloudProvider>? logger = null)
    {
        _logger = logger;
    }

    public string? SessionId => _sessionId;
    string ICommandSender.SessionId => _sessionId ?? throw new InvalidOperationException("No session ID available");
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

        // Create type-safe proxy for calling hub methods (Client → Server)
        _hubProxy = _connection.CreateHubProxy<IClientHubServer>();
        
        // Register this instance as receiver for incoming calls (Server → Client)
        _receiverSubscription = _connection.Register<IClientHubClient>(this);

        // Handle reconnection - resume session with new connection ID
        _connection.Reconnecting += OnReconnectingAsync;
        _connection.Reconnected += OnReconnectedAsync;
        _connection.Closed += OnClosedAsync;

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
            ImageId = options.ImageId,
            Tags = options.Tags
        };

        var createResponse = await _hubProxy.CreateSession(createRequest);

        _sessionId = createResponse.SessionId;
        _logger?.LogInformation("Session created: {SessionId}", _sessionId);
    }

    public async Task<RdpAccessInfo> GetRdpAccessAsync(TimeSpan? duration = null, CancellationToken cancelToken = default)
    {
        if (_hubProxy == null)
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

        var rdpInfo = await InvokeWithRetryAsync(
            () => _hubProxy.GetRdpAccess(_sessionId, duration),
            nameof(IClientHubServer.GetRdpAccess),
            cancelToken);

        _logger?.LogInformation("RDP access granted, expires at {ExpiresAt}", rdpInfo.ExpiresAt);

        return rdpInfo;
    }

    #region ICommandSender Implementation

    public async Task<T?> SendCommandAsync<T>(ComputerCommand command, CancellationToken cancelToken = default)
    {
        if (_hubProxy == null)
        {
            throw new InvalidOperationException("Not connected. Call ConnectAsync first.");
        }

        if (_state != ComputerState.Ready)
        {
            throw new InvalidOperationException($"Computer is not ready (current state: {_state})");
        }

        var tcs = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pendingRequests[command.CorrelationId] = tcs;

        try
        {
            _logger?.LogDebug("Sending command {CommandType} (CorrelationId: {CorrelationId})",
                command.GetType().Name, command.CorrelationId);

            await InvokeWithRetryAsync(
                () => _hubProxy.SendCommand(command),
                "SendCommand",
                cancelToken);

            // Wait for response with timeout
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancelToken, timeoutCts.Token);

            var result = await tcs.Task.WaitAsync(linkedCts.Token);

            if (result == null)
            {
                return default;
            }

            // Result is a JsonElement, deserialize to the expected type
            if (result is JsonElement jsonElement)
            {
                return jsonElement.Deserialize<T>();
            }

            return (T?)result;
        }
        catch (OperationCanceledException) when (!cancelToken.IsCancellationRequested)
        {
            throw new TimeoutException($"Command {command.GetType().Name} timed out after 2 minutes");
        }
        finally
        {
            _pendingRequests.TryRemove(command.CorrelationId, out _);
        }
    }

    public async Task SendCommandAsync(ComputerCommand command, CancellationToken cancelToken = default)
    {
        await SendCommandAsync<object>(command, cancelToken);
    }

    #endregion

    /// <summary>
    /// Invokes a hub method with automatic retry on connection failures.
    /// </summary>
    private async Task<TResult> InvokeWithRetryAsync<TResult>(
        Func<Task<TResult>> action,
        string methodName,
        CancellationToken cancelToken)
    {
        if (_connection == null)
        {
            throw new InvalidOperationException("Not connected.");
        }

        Exception? lastException = null;

        for (var attempt = 1; attempt <= MaxRetries; attempt++)
        {
            try
            {
                // Only wait for reconnection if we're actually in a reconnecting state
                if (_connection.State == HubConnectionState.Reconnecting)
                {
                    var reconnectingTcs = _reconnectedTcs;
                    if (reconnectingTcs != null)
                    {
                        _logger?.LogInformation(
                            "Connection is reconnecting, waiting before {Method} attempt {Attempt}",
                            methodName, attempt);
                        
                        using var timeoutCts = new CancellationTokenSource(ReconnectWaitTimeout);
                        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                            cancelToken, timeoutCts.Token);
                        
                        try
                        {
                            await reconnectingTcs.Task.WaitAsync(linkedCts.Token);
                        }
                        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
                        {
                            throw new TimeoutException(
                                $"Timed out waiting for reconnection before {methodName}");
                        }
                    }
                }
                else if (_connection.State == HubConnectionState.Disconnected)
                {
                    throw new InvalidOperationException("Connection is disconnected.");
                }

                _logger?.LogDebug("Invoking {Method} (attempt {Attempt}/{MaxRetries})", methodName, attempt, MaxRetries);
                return await action();
            }
            catch (Exception ex) when (IsConnectionLostException(ex) && attempt < MaxRetries)
            {
                lastException = ex;
                _logger?.LogWarning(
                    "Connection lost during {Method} (attempt {Attempt}/{MaxRetries}), waiting for reconnection...",
                    methodName, attempt, MaxRetries);

                // Wait for the reconnection to complete
                var reconnectTcs = _reconnectedTcs;
                if (reconnectTcs != null && _connection.State == HubConnectionState.Reconnecting)
                {
                    using var timeoutCts = new CancellationTokenSource(ReconnectWaitTimeout);
                    using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                        cancelToken, timeoutCts.Token);
                    
                    try
                    {
                        await reconnectTcs.Task.WaitAsync(linkedCts.Token);
                        _logger?.LogInformation(
                            "Reconnection complete, retrying {Method} (attempt {Attempt}/{MaxRetries})",
                            methodName, attempt + 1, MaxRetries);
                    }
                    catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
                    {
                        throw new TimeoutException(
                            $"Timed out waiting for reconnection during {methodName}: {ex.Message}", ex);
                    }
                }
                else
                {
                    // No reconnection in progress - wait a bit and retry
                    _logger?.LogInformation(
                        "No reconnection in progress (state: {State}), waiting 2s before retry",
                        _connection.State);
                    await Task.Delay(TimeSpan.FromSeconds(2), cancelToken);
                }
            }
        }

        // If we get here, all retries failed
        throw new InvalidOperationException(
            $"Failed to invoke {methodName} after {MaxRetries} attempts",
            lastException);
    }

    /// <summary>
    /// Invokes a hub method with automatic retry on connection failures (for void-returning methods).
    /// </summary>
    private async Task InvokeWithRetryAsync(
        Func<Task> action,
        string methodName,
        CancellationToken cancelToken)
    {
        await InvokeWithRetryAsync<object?>(async () =>
        {
            await action();
            return null;
        }, methodName, cancelToken);
    }

    /// <summary>
    /// Determines if an exception indicates a connection loss that can be retried.
    /// </summary>
    private static bool IsConnectionLostException(Exception ex)
    {
        var message = ex.Message;
        
        return message.Contains("Server connection which the client routed to is closed", StringComparison.OrdinalIgnoreCase)
            || message.Contains("connection was terminated", StringComparison.OrdinalIgnoreCase)
            || message.Contains("connection closed", StringComparison.OrdinalIgnoreCase)
            || message.Contains("The server returned status code", StringComparison.OrdinalIgnoreCase)
            || ex is InvalidOperationException { Message: "The 'InvokeCoreAsync' method cannot be called" };
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection != null)
        {
            _logger?.LogInformation("Disposing CloudProvider and ending session {SessionId}", _sessionId);
            
            // Unregister all event handlers
            _connection.Reconnecting -= OnReconnectingAsync;
            _connection.Reconnected -= OnReconnectedAsync;
            _connection.Closed -= OnClosedAsync;
            
            // Dispose the receiver subscription
            _receiverSubscription?.Dispose();
            _receiverSubscription = null;
            
            // Clear any pending TCS to unblock waiting operations
            _reconnectedTcs?.TrySetCanceled();
            _reconnectedTcs = null;
            
            try
            {
                if (_sessionId != null && _hubProxy != null && _connection.State == HubConnectionState.Connected)
                {
                    await _hubProxy.EndSession(_sessionId);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to end session gracefully");
            }
            
            await _connection.DisposeAsync();
            _connection = null;
            _hubProxy = null;
        }
    }

    private Task OnReconnectingAsync(Exception? error)
    {
        _logger?.LogWarning(error,
            "Connection lost, attempting to reconnect for session {SessionId}",
            _sessionId);
        
        // Create a TCS that pending operations can wait on
        _reconnectedTcs ??= new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        
        return Task.CompletedTask;
    }

    private Task OnClosedAsync(Exception? error)
    {
        _logger?.LogWarning(error,
            "Connection closed permanently for session {SessionId}",
            _sessionId);
        
        // Signal any waiting operations that reconnection failed
        var tcs = _reconnectedTcs;
        _reconnectedTcs = null;
        
        if (tcs != null)
        {
            tcs.TrySetException(error ?? new InvalidOperationException("Connection closed"));
        }
        
        return Task.CompletedTask;
    }

    private async Task OnReconnectedAsync(string? connectionId)
    {
        if (_sessionId == null || _hubProxy == null)
        {
            _logger?.LogWarning("Reconnected but no session ID to resume");
            _reconnectedTcs?.TrySetResult();
            _reconnectedTcs = null;
            return;
        }

        try
        {
            _logger?.LogInformation(
                "Reconnected to API (connection: {ConnectionId}), resuming session {SessionId}",
                connectionId, _sessionId);

            var response = await _hubProxy.ResumeSession(_sessionId);

            _logger?.LogInformation(
                "Session {SessionId} resumed successfully with status {Status}",
                _sessionId, response.Status);
            
            // Signal that reconnection is complete - pending operations can now retry
            _reconnectedTcs?.TrySetResult();
            _reconnectedTcs = null;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex,
                "Failed to resume session {SessionId} after reconnection",
                _sessionId);
            
            // Signal reconnection failure
            _reconnectedTcs?.TrySetException(ex);
            _reconnectedTcs = null;
            
            // Update state to error
            UpdateState(ComputerState.Error);
            _sessionReadyTcs.TrySetException(new InvalidOperationException(
                $"Failed to resume session after reconnection: {ex.Message}", ex));
        }
    }

    #region IClientHubClient Implementation (Server → Client calls)

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

    public Task DiskImageCaptured(string sessionId, DiskImageInfo imageInfo, string correlationId)
    {
        _logger?.LogInformation("Disk image captured: {ImageId} for session {SessionId} ({SizeBytes:N0} bytes)",
            imageInfo.ImageId, sessionId, imageInfo.SizeBytes);
        
        if (_pendingRequests.TryRemove(correlationId, out var tcs))
        {
            tcs.TrySetResult(imageInfo);
        }
        
        return Task.CompletedTask;
    }
    
    #endregion

    /// <summary>
    /// Waits for the session to become ready (after ConnectAsync).
    /// </summary>
    public Task WaitForReadyAsync(CancellationToken cancelToken = default)
    {
        return _sessionReadyTcs.Task.WaitAsync(cancelToken);
    }

    public async Task<DiskImageInfo> SaveAsDiskImageAsync(SaveDiskImageOptions options, CancellationToken cancelToken = default)
    {
        if (_sessionId == null || _hubProxy == null)
        {
            throw new InvalidOperationException("Not connected to a session");
        }

        _logger?.LogInformation("Saving session {SessionId} as disk image '{Name}'", _sessionId, options.Name);

        var correlationId = Guid.NewGuid().ToString("N");
        var tcs = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pendingRequests[correlationId] = tcs;

        try
        {
            // Send the capture request with our correlation ID
            var imageId = await InvokeWithRetryAsync(
                () => _hubProxy.SaveAsDiskImage(_sessionId, options, correlationId),
                nameof(IClientHubServer.SaveAsDiskImage),
                cancelToken);

            _logger?.LogInformation("Disk image capture initiated for session {SessionId}, imageId: {ImageId}", 
                _sessionId, imageId);

            // Wait for the DiskImageCaptured callback
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMinutes(30)); // Capture can take a while
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancelToken, timeoutCts.Token);

            var result = await tcs.Task.WaitAsync(linkedCts.Token);

            if (result is DiskImageInfo imageInfo)
            {
                _logger?.LogInformation("Disk image captured successfully: {ImageId}", imageInfo.ImageId);
                
                // Session is ended after capture
                UpdateState(ComputerState.Stopped);
                
                return imageInfo;
            }

            throw new InvalidOperationException("Unexpected response type from DiskImageCaptured callback");
        }
        catch (OperationCanceledException) when (cancelToken.IsCancellationRequested)
        {
            _pendingRequests.TryRemove(correlationId, out _);
            throw;
        }
        catch (OperationCanceledException)
        {
            _pendingRequests.TryRemove(correlationId, out _);
            throw new TimeoutException("Disk image capture timed out after 30 minutes");
        }
        catch
        {
            _pendingRequests.TryRemove(correlationId, out _);
            throw;
        }
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


