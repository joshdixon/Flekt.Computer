using Flekt.Computer.Abstractions.Contracts;

namespace Flekt.Computer.Interface;

/// <summary>
/// Interface for sending commands to the remote computer.
/// Implemented by CloudProvider.
/// </summary>
internal interface ICommandSender
{
    /// <summary>
    /// Gets the current session ID.
    /// </summary>
    string SessionId { get; }

    /// <summary>
    /// Sends a command and waits for the response.
    /// </summary>
    Task<T?> SendCommandAsync<T>(ComputerCommand command, CancellationToken cancelToken = default);

    /// <summary>
    /// Sends a command that doesn't return a result.
    /// </summary>
    Task SendCommandAsync(ComputerCommand command, CancellationToken cancelToken = default);
}










