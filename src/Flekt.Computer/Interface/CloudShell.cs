using Flekt.Computer.Abstractions;
using Flekt.Computer.Abstractions.Contracts;

namespace Flekt.Computer.Interface;

internal sealed class CloudShell : IShell
{
    private readonly ICommandSender _sender;

    public CloudShell(ICommandSender sender)
    {
        _sender = sender;
    }

    public async Task<CommandResult> Run(string command, CancellationToken cancelToken = default)
    {
        var result = await _sender.SendCommandAsync<CommandResult>(new ShellRunCommand
        {
            SessionId = _sender.SessionId,
            CorrelationId = Guid.NewGuid().ToString(),
            Command = command,
            Timeout = null
        }, cancelToken);

        return result ?? CommandResult.Fail(-1, "No response received");
    }

    public async Task<CommandResult> Run(string command, TimeSpan timeout, CancellationToken cancelToken = default)
    {
        var result = await _sender.SendCommandAsync<CommandResult>(new ShellRunCommand
        {
            SessionId = _sender.SessionId,
            CorrelationId = Guid.NewGuid().ToString(),
            Command = command,
            Timeout = timeout
        }, cancelToken);

        return result ?? CommandResult.Fail(-1, "No response received");
    }
}
