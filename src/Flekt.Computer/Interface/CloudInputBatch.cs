using Flekt.Computer.Abstractions;
using Flekt.Computer.Abstractions.Contracts;

namespace Flekt.Computer.Interface;

internal sealed class CloudInputBatch : IInputBatch
{
    private readonly ICommandSender _sender;

    public CloudInputBatch(ICommandSender sender)
    {
        _sender = sender;
    }

    public async Task<InputBatchResult> ExecuteAsync(
        IReadOnlyList<BatchedInput> inputs,
        CancellationToken cancelToken = default)
    {
        if (inputs.Count == 0)
        {
            return new InputBatchResult { Attempted = 0, Succeeded = 0 };
        }

        var result = await _sender.SendCommandAsync<InputBatchResult>(new InputBatchCommand
        {
            SessionId = _sender.SessionId,
            CorrelationId = Guid.NewGuid().ToString(),
            Inputs = inputs
        }, cancelToken);

        // Defensive — the Agent always returns a non-null InputBatchResult, but
        // if a transport / serialization issue produced null, surface that as a
        // result rather than crashing the caller.
        return result ?? new InputBatchResult
        {
            Attempted = inputs.Count,
            Succeeded = 0,
            Errors = [new BatchedInputError
            {
                Index = -1,
                CommandType = nameof(InputBatchCommand),
                Message = "Agent returned a null result"
            }]
        };
    }
}
