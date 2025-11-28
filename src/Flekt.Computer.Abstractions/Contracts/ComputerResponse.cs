using System.Text.Json;

namespace Flekt.Computer.Abstractions.Contracts;

/// <summary>
/// Response from executing a command.
/// </summary>
public sealed record ComputerResponse
{
    /// <summary>
    /// The session ID this response belongs to.
    /// </summary>
    public required string SessionId { get; init; }

    /// <summary>
    /// Correlation ID matching the original command.
    /// </summary>
    public required string CorrelationId { get; init; }

    /// <summary>
    /// Whether the command succeeded.
    /// </summary>
    public required bool Success { get; init; }

    /// <summary>
    /// Result data as JSON (type depends on command).
    /// </summary>
    public JsonElement? Result { get; init; }

    /// <summary>
    /// Error code (if failed).
    /// </summary>
    public string? ErrorCode { get; init; }

    /// <summary>
    /// Error message (if failed).
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// How long the command took to execute.
    /// </summary>
    public TimeSpan Duration { get; init; }
}

/// <summary>
/// Exception thrown when a command fails.
/// </summary>
public class ComputerCommandException : Exception
{
    public string ErrorCode { get; }

    public ComputerCommandException(string errorCode, string message)
        : base(message)
    {
        ErrorCode = errorCode;
    }

    public ComputerCommandException(string errorCode, string message, Exception innerException)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
    }
}

