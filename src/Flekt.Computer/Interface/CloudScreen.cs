using Flekt.Computer.Abstractions;
using Flekt.Computer.Abstractions.Contracts;

namespace Flekt.Computer.Interface;

internal sealed class CloudScreen : IScreen
{
    private readonly ICommandSender _sender;

    public CloudScreen(ICommandSender sender)
    {
        _sender = sender;
    }

    public async Task<byte[]> Screenshot(CancellationToken cancelToken = default)
    {
        var result = await _sender.SendCommandAsync<string>(new ScreenScreenshotCommand
        {
            SessionId = _sender.SessionId,
            CorrelationId = Guid.NewGuid().ToString()
        }, cancelToken);

        return result != null ? Convert.FromBase64String(result) : Array.Empty<byte>();
    }

    public async Task<ScreenSize> GetSize(CancellationToken cancelToken = default)
    {
        var result = await _sender.SendCommandAsync<ScreenSize>(new ScreenGetSizeCommand
        {
            SessionId = _sender.SessionId,
            CorrelationId = Guid.NewGuid().ToString()
        }, cancelToken);

        return result;
    }
}
