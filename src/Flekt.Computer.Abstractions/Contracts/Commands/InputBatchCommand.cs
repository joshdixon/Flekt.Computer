namespace Flekt.Computer.Abstractions.Contracts;

/// <summary>
/// Batches a sequence of input commands (keyboard, mouse, clipboard) into a
/// single round-trip. The Agent replays each item with its own preceding
/// delay, then returns a single <see cref="InputBatchResult"/> when the whole
/// sequence is done.
///
/// Use this to avoid per-keystroke RTT during recorded-input replay. For
/// 12 keystrokes with 50-150ms RTT each, this cuts the WAN cost from
/// 600-1800ms to one round-trip.
/// </summary>
public sealed record InputBatchCommand : ComputerCommand
{
    /// <summary>The inputs to execute, in order.</summary>
    public required IReadOnlyList<BatchedInput> Inputs { get; init; }
}

/// <summary>
/// A single input within an <see cref="InputBatchCommand"/>: the delay to
/// wait *before* executing the input, then the input command itself.
/// </summary>
public sealed record BatchedInput
{
    /// <summary>
    /// Milliseconds to wait before executing <see cref="Input"/>. Use 0 for
    /// "fire as fast as possible". The Agent caps individual delays at 5000ms
    /// to bound the worst-case batch duration even with a malformed payload.
    /// </summary>
    public required int DelayMs { get; init; }

    /// <summary>
    /// The input command to execute. Must be one of the keyboard/mouse/clipboard
    /// commands defined on <see cref="ComputerCommand"/>'s polymorphic
    /// discriminator list. SessionId and CorrelationId on the inner command
    /// are ignored — the outer batch command's values are authoritative.
    /// </summary>
    public required ComputerCommand Input { get; init; }
}

/// <summary>
/// Result of executing an <see cref="InputBatchCommand"/>. Returned in the
/// <c>Result</c> field of the outer <see cref="ComputerResponse"/>.
/// </summary>
public sealed record InputBatchResult
{
    /// <summary>Number of inputs the Agent attempted to execute.</summary>
    public required int Attempted { get; init; }

    /// <summary>Number of inputs that completed without error.</summary>
    public required int Succeeded { get; init; }

    /// <summary>
    /// Per-input errors. Index refers to the position in the original
    /// <see cref="InputBatchCommand.Inputs"/> list. The Agent does not stop
    /// on an error — it logs and continues, matching the existing per-event
    /// replay behavior.
    /// </summary>
    public IReadOnlyList<BatchedInputError> Errors { get; init; } = [];
}

public sealed record BatchedInputError
{
    public required int Index { get; init; }
    public required string CommandType { get; init; }
    public required string Message { get; init; }
}
