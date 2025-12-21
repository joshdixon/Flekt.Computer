using Flekt.Computer.Abstractions;
using Flekt.Computer.Abstractions.Contracts;

namespace Flekt.Computer.Interface;

internal sealed class CloudMouse : IMouse
{
    private readonly ICommandSender _sender;

    public CloudMouse(ICommandSender sender)
    {
        _sender = sender;
    }

    public Task LeftClick(int? x = null, int? y = null, CancellationToken cancelToken = default)
    {
        return _sender.SendCommandAsync(new MouseLeftClickCommand
        {
            SessionId = _sender.SessionId,
            CorrelationId = Guid.NewGuid().ToString(),
            X = x,
            Y = y
        }, cancelToken);
    }

    public Task RightClick(int? x = null, int? y = null, CancellationToken cancelToken = default)
    {
        return _sender.SendCommandAsync(new MouseRightClickCommand
        {
            SessionId = _sender.SessionId,
            CorrelationId = Guid.NewGuid().ToString(),
            X = x,
            Y = y
        }, cancelToken);
    }

    public Task DoubleClick(int? x = null, int? y = null, CancellationToken cancelToken = default)
    {
        return _sender.SendCommandAsync(new MouseDoubleClickCommand
        {
            SessionId = _sender.SessionId,
            CorrelationId = Guid.NewGuid().ToString(),
            X = x,
            Y = y
        }, cancelToken);
    }

    public Task Move(int x, int y, CancellationToken cancelToken = default)
    {
        return _sender.SendCommandAsync(new MouseMoveCommand
        {
            SessionId = _sender.SessionId,
            CorrelationId = Guid.NewGuid().ToString(),
            X = x,
            Y = y
        }, cancelToken);
    }

    public Task Down(int? x = null, int? y = null, MouseButton button = MouseButton.Left, CancellationToken cancelToken = default)
    {
        return _sender.SendCommandAsync(new MouseDownCommand
        {
            SessionId = _sender.SessionId,
            CorrelationId = Guid.NewGuid().ToString(),
            X = x,
            Y = y,
            Button = button
        }, cancelToken);
    }

    public Task Up(int? x = null, int? y = null, MouseButton button = MouseButton.Left, CancellationToken cancelToken = default)
    {
        return _sender.SendCommandAsync(new MouseUpCommand
        {
            SessionId = _sender.SessionId,
            CorrelationId = Guid.NewGuid().ToString(),
            X = x,
            Y = y,
            Button = button
        }, cancelToken);
    }

    public Task MovePath(IEnumerable<MousePathPoint> path, MousePathOptions? options = null, CancellationToken cancelToken = default)
    {
        return _sender.SendCommandAsync(new MouseMovePathCommand
        {
            SessionId = _sender.SessionId,
            CorrelationId = Guid.NewGuid().ToString(),
            Path = path.ToList(),
            Options = options
        }, cancelToken);
    }

    public Task Drag(IEnumerable<MousePathPoint> path, MouseButton button = MouseButton.Left, MousePathOptions? options = null, CancellationToken cancelToken = default)
    {
        return _sender.SendCommandAsync(new MouseDragCommand
        {
            SessionId = _sender.SessionId,
            CorrelationId = Guid.NewGuid().ToString(),
            Path = path.ToList(),
            Button = button,
            Options = options
        }, cancelToken);
    }

    public Task DragTo(int x, int y, MouseButton button = MouseButton.Left, CancellationToken cancelToken = default)
    {
        return _sender.SendCommandAsync(new MouseDragToCommand
        {
            SessionId = _sender.SessionId,
            CorrelationId = Guid.NewGuid().ToString(),
            X = x,
            Y = y,
            Button = button
        }, cancelToken);
    }

    public Task Scroll(int deltaX, int deltaY, CancellationToken cancelToken = default)
    {
        return _sender.SendCommandAsync(new MouseScrollCommand
        {
            SessionId = _sender.SessionId,
            CorrelationId = Guid.NewGuid().ToString(),
            DeltaX = deltaX,
            DeltaY = deltaY
        }, cancelToken);
    }

    public Task ScrollDown(int clicks = 1, CancellationToken cancelToken = default)
    {
        return Scroll(0, -clicks * 120, cancelToken);
    }

    public Task ScrollUp(int clicks = 1, CancellationToken cancelToken = default)
    {
        return Scroll(0, clicks * 120, cancelToken);
    }

    public async Task<CursorPosition> GetPosition(CancellationToken cancelToken = default)
    {
        var result = await _sender.SendCommandAsync<CursorPosition>(new MouseGetPositionCommand
        {
            SessionId = _sender.SessionId,
            CorrelationId = Guid.NewGuid().ToString()
        }, cancelToken);

        return result;
    }
}
