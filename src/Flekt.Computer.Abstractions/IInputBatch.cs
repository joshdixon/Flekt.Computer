using Flekt.Computer.Abstractions.Contracts;

namespace Flekt.Computer.Abstractions;

/// <summary>
/// Executes a sequence of input commands (mouse, keyboard, clipboard) with
/// per-item delays in a single round-trip to the Agent. Use this instead of
/// individually awaited <see cref="IMouse"/>/<see cref="IKeyboard"/> calls
/// when replaying recorded input — collapses N command round-trips into 1.
/// </summary>
public interface IInputBatch
{
    /// <summary>
    /// Sends the batch to the Agent. The Agent applies each item's
    /// <see cref="BatchedInput.DelayMs"/> before executing its
    /// <see cref="BatchedInput.Input"/>, in order, and returns a summary
    /// when the whole sequence is done. Errors on individual items do not
    /// abort the batch — the Agent logs and continues.
    /// </summary>
    Task<InputBatchResult> ExecuteAsync(
        IReadOnlyList<BatchedInput> inputs,
        CancellationToken cancelToken = default);
}
