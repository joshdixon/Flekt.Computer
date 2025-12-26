using Flekt.Computer.Abstractions.Contracts;
using Flekt.Computer.Abstractions.Models;

using TypedSignalR.Client;

namespace Flekt.Computer.Abstractions;

/// <summary>
/// SignalR callback interface that the API invokes on SDK clients.
/// SDK implementations listen for these callbacks to handle state changes and responses.
/// The [Receiver] attribute generates type-safe handler registration via source generation.
/// </summary>
[Receiver]
public interface IClientHubClient
{
    /// <summary>
    /// Notifies the client that their session is ready (VM started, agent connected).
    /// </summary>
    Task SessionReady(string sessionId);

    /// <summary>
    /// Notifies the client of session state changes.
    /// </summary>
    Task SessionStateChanged(string sessionId, string state);

    /// <summary>
    /// Sends a command response back to the client.
    /// SDK matches correlationId to complete the pending Task.
    /// </summary>
    Task CommandResponse(ComputerResponse response);

    /// <summary>
    /// Notifies the client that a checkpoint was created.
    /// </summary>
    Task CheckpointCreated(string sessionId, string checkpointId, string correlationId);

    /// <summary>
    /// Notifies the client that a checkpoint was restored.
    /// </summary>
    Task CheckpointRestored(string sessionId, string correlationId);

    /// <summary>
    /// Notifies the client of an error.
    /// </summary>
    Task Error(string sessionId, string errorCode, string message);

    /// <summary>
    /// Notifies the client that a disk image was captured successfully.
    /// </summary>
    Task DiskImageCaptured(string sessionId, DiskImageInfo imageInfo, string correlationId);

    /// <summary>
    /// Real-time input event from session recording (50ms throttled for mouse moves).
    /// Events are streamed as they occur for live cursor/input visualization.
    /// </summary>
    Task InputEventReceived(string sessionId, InputEventData inputEvent);
}





