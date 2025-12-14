using Flekt.Computer.Abstractions.Contracts;

using TypedSignalR.Client;

namespace Flekt.Computer.Abstractions;

/// <summary>
/// Methods the SDK client can call on the API via SignalR (Client → Server).
/// This is the inverse of <see cref="IClientHubClient"/> (Server → Client).
/// The [Hub] attribute generates a type-safe client proxy via source generation.
/// </summary>
[Hub]
public interface IClientHubServer
{
    /// <summary>
    /// Creates a new computer session.
    /// </summary>
    Task<CreateSessionResponse> CreateSession(CreateSessionRequest request);

    /// <summary>
    /// Resumes an existing session after reconnection.
    /// </summary>
    Task<CreateSessionResponse> ResumeSession(string sessionId);

    /// <summary>
    /// Sends a command (mouse click, keyboard type, etc.) to the computer.
    /// Fire-and-forget: response comes back via CommandResponse callback.
    /// </summary>
    Task SendCommand(ComputerCommand command);

    /// <summary>
    /// Requests RDP access credentials for the session.
    /// </summary>
    Task<RdpAccessInfo> GetRdpAccess(string sessionId, TimeSpan? duration);

    /// <summary>
    /// Creates a checkpoint (snapshot) of the current VM state.
    /// Fire-and-forget: completion notified via CheckpointCreated callback.
    /// </summary>
    Task CreateCheckpoint(string sessionId, string? name, string correlationId);

    /// <summary>
    /// Restores a previously created checkpoint.
    /// Fire-and-forget: completion notified via CheckpointRestored callback.
    /// </summary>
    Task RestoreCheckpoint(string sessionId, string checkpointId, string correlationId);

    /// <summary>
    /// Saves the current state as a reusable Environment.
    /// </summary>
    Task<EnvironmentInfo> SaveAsEnvironment(string sessionId, SaveEnvironmentOptions options);

    /// <summary>
    /// Ends the session and cleans up resources.
    /// </summary>
    Task EndSession(string sessionId);
}
