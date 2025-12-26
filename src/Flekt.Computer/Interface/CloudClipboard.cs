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

    #region Text

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

    #endregion

    #region Files

    public Task SetFiles(IEnumerable<string> urls, CancellationToken cancelToken = default)
    {
        return _sender.SendCommandAsync(new ClipboardSetFilesCommand
        {
            SessionId = _sender.SessionId,
            CorrelationId = Guid.NewGuid().ToString(),
            Urls = urls.ToArray()
        }, cancelToken);
    }

    public Task SetFilesFromPaths(IEnumerable<string> localPaths, CancellationToken cancelToken = default)
    {
        return _sender.SendCommandAsync(new ClipboardSetFilesFromPathsCommand
        {
            SessionId = _sender.SessionId,
            CorrelationId = Guid.NewGuid().ToString(),
            Paths = localPaths.ToArray()
        }, cancelToken);
    }

    public async Task<string[]?> GetFiles(CancellationToken cancelToken = default)
    {
        return await _sender.SendCommandAsync<string[]?>(new ClipboardGetFilesCommand
        {
            SessionId = _sender.SessionId,
            CorrelationId = Guid.NewGuid().ToString()
        }, cancelToken);
    }

    #endregion

    #region Images

    public Task SetImage(string url, CancellationToken cancelToken = default)
    {
        return _sender.SendCommandAsync(new ClipboardSetImageFromUrlCommand
        {
            SessionId = _sender.SessionId,
            CorrelationId = Guid.NewGuid().ToString(),
            Url = url
        }, cancelToken);
    }

    public Task SetImage(byte[] imageData, CancellationToken cancelToken = default)
    {
        return _sender.SendCommandAsync(new ClipboardSetImageFromBytesCommand
        {
            SessionId = _sender.SessionId,
            CorrelationId = Guid.NewGuid().ToString(),
            ImageBase64 = Convert.ToBase64String(imageData)
        }, cancelToken);
    }

    public async Task<byte[]?> GetImage(CancellationToken cancelToken = default)
    {
        string? base64 = await _sender.SendCommandAsync<string?>(new ClipboardGetImageCommand
        {
            SessionId = _sender.SessionId,
            CorrelationId = Guid.NewGuid().ToString()
        }, cancelToken);

        return base64 != null ? Convert.FromBase64String(base64) : null;
    }

    #endregion

    #region Content Type

    public async Task<ClipboardContentType> GetContentType(CancellationToken cancelToken = default)
    {
        string? result = await _sender.SendCommandAsync<string?>(new ClipboardGetContentTypeCommand
        {
            SessionId = _sender.SessionId,
            CorrelationId = Guid.NewGuid().ToString()
        }, cancelToken);

        return Enum.TryParse<ClipboardContentType>(result, out var contentType)
            ? contentType
            : ClipboardContentType.Empty;
    }

    #endregion
}
