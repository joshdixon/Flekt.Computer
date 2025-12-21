using Flekt.Computer.Abstractions;
using Flekt.Computer.Abstractions.Contracts;

namespace Flekt.Computer.Interface;

internal sealed class CloudClipboard : IClipboard
{
    private readonly ICommandSender _sender;

    public CloudClipboard(ICommandSender sender)
    {
        _sender = sender;
    }

    public async Task<string> Get(CancellationToken cancelToken = default)
    {
        var result = await _sender.SendCommandAsync<string>(new ClipboardGetCommand
        {
            SessionId = _sender.SessionId,
            CorrelationId = Guid.NewGuid().ToString()
        }, cancelToken);

        return result ?? string.Empty;
    }

    public Task Set(string text, CancellationToken cancelToken = default)
    {
        return _sender.SendCommandAsync(new ClipboardSetCommand
        {
            SessionId = _sender.SessionId,
            CorrelationId = Guid.NewGuid().ToString(),
            Text = text
        }, cancelToken);
    }
}
