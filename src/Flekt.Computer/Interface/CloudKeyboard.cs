using Flekt.Computer.Abstractions;
using Flekt.Computer.Abstractions.Contracts;

namespace Flekt.Computer.Interface;

internal sealed class CloudKeyboard : IKeyboard
{
    private readonly ICommandSender _sender;

    public CloudKeyboard(ICommandSender sender)
    {
        _sender = sender;
    }

    public Task Type(string text, CancellationToken cancelToken = default)
    {
        return _sender.SendCommandAsync(new KeyboardTypeCommand
        {
            SessionId = _sender.SessionId,
            CorrelationId = Guid.NewGuid().ToString(),
            Text = text
        }, cancelToken);
    }

    public Task Press(string key, CancellationToken cancelToken = default)
    {
        return _sender.SendCommandAsync(new KeyboardPressCommand
        {
            SessionId = _sender.SessionId,
            CorrelationId = Guid.NewGuid().ToString(),
            Key = key
        }, cancelToken);
    }

    public Task Down(string key, CancellationToken cancelToken = default)
    {
        return _sender.SendCommandAsync(new KeyboardDownCommand
        {
            SessionId = _sender.SessionId,
            CorrelationId = Guid.NewGuid().ToString(),
            Key = key
        }, cancelToken);
    }

    public Task Up(string key, CancellationToken cancelToken = default)
    {
        return _sender.SendCommandAsync(new KeyboardUpCommand
        {
            SessionId = _sender.SessionId,
            CorrelationId = Guid.NewGuid().ToString(),
            Key = key
        }, cancelToken);
    }

    public Task Hotkey(CancellationToken cancelToken = default, params string[] keys)
    {
        return _sender.SendCommandAsync(new KeyboardHotkeyCommand
        {
            SessionId = _sender.SessionId,
            CorrelationId = Guid.NewGuid().ToString(),
            Keys = keys.ToList()
        }, cancelToken);
    }
}
