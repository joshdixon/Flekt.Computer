using Flekt.Computer.Abstractions;
using Flekt.Computer.Abstractions.Contracts;

namespace Flekt.Computer.Interface;

internal sealed class CloudWindows : IWindows
{
    private readonly ICommandSender _sender;

    public CloudWindows(ICommandSender sender)
    {
        _sender = sender;
    }

    public async Task<string> GetActiveId(CancellationToken cancelToken = default)
    {
        var result = await _sender.SendCommandAsync<string>(new WindowGetActiveIdCommand
        {
            SessionId = _sender.SessionId,
            CorrelationId = Guid.NewGuid().ToString()
        }, cancelToken);

        return result ?? string.Empty;
    }

    public async Task<WindowInfo> GetInfo(string windowId, CancellationToken cancelToken = default)
    {
        var result = await _sender.SendCommandAsync<WindowInfo>(new WindowGetInfoCommand
        {
            SessionId = _sender.SessionId,
            CorrelationId = Guid.NewGuid().ToString(),
            WindowId = windowId
        }, cancelToken);

        return result ?? new WindowInfo { Id = windowId, Title = "" };
    }

    public Task Activate(string windowId, CancellationToken cancelToken = default)
    {
        return _sender.SendCommandAsync(new WindowActivateCommand
        {
            SessionId = _sender.SessionId,
            CorrelationId = Guid.NewGuid().ToString(),
            WindowId = windowId
        }, cancelToken);
    }

    public Task Close(string windowId, CancellationToken cancelToken = default)
    {
        return _sender.SendCommandAsync(new WindowCloseCommand
        {
            SessionId = _sender.SessionId,
            CorrelationId = Guid.NewGuid().ToString(),
            WindowId = windowId
        }, cancelToken);
    }

    public Task Maximize(string windowId, CancellationToken cancelToken = default)
    {
        return _sender.SendCommandAsync(new WindowMaximizeCommand
        {
            SessionId = _sender.SessionId,
            CorrelationId = Guid.NewGuid().ToString(),
            WindowId = windowId
        }, cancelToken);
    }

    public Task Minimize(string windowId, CancellationToken cancelToken = default)
    {
        return _sender.SendCommandAsync(new WindowMinimizeCommand
        {
            SessionId = _sender.SessionId,
            CorrelationId = Guid.NewGuid().ToString(),
            WindowId = windowId
        }, cancelToken);
    }

    public Task Restore(string windowId, CancellationToken cancelToken = default)
    {
        return _sender.SendCommandAsync(new WindowRestoreCommand
        {
            SessionId = _sender.SessionId,
            CorrelationId = Guid.NewGuid().ToString(),
            WindowId = windowId
        }, cancelToken);
    }

    public async Task<IReadOnlyList<WindowInfo>> List(CancellationToken cancelToken = default)
    {
        var result = await _sender.SendCommandAsync<List<WindowInfo>>(new WindowListCommand
        {
            SessionId = _sender.SessionId,
            CorrelationId = Guid.NewGuid().ToString()
        }, cancelToken);

        return result ?? new List<WindowInfo>();
    }
}
