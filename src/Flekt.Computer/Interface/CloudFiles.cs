using Flekt.Computer.Abstractions;
using Flekt.Computer.Abstractions.Contracts;

namespace Flekt.Computer.Interface;

internal sealed class CloudFiles : IFiles
{
    private readonly ICommandSender _sender;

    public CloudFiles(ICommandSender sender)
    {
        _sender = sender;
    }

    public async Task<bool> Exists(string path, CancellationToken cancelToken = default)
    {
        var result = await _sender.SendCommandAsync<bool>(new FileExistsCommand
        {
            SessionId = _sender.SessionId,
            CorrelationId = Guid.NewGuid().ToString(),
            Path = path
        }, cancelToken);

        return result;
    }

    public async Task<string> ReadText(string path, CancellationToken cancelToken = default)
    {
        var result = await _sender.SendCommandAsync<string>(new FileReadTextCommand
        {
            SessionId = _sender.SessionId,
            CorrelationId = Guid.NewGuid().ToString(),
            Path = path
        }, cancelToken);

        return result ?? string.Empty;
    }

    public Task WriteText(string path, string content, CancellationToken cancelToken = default)
    {
        return _sender.SendCommandAsync(new FileWriteTextCommand
        {
            SessionId = _sender.SessionId,
            CorrelationId = Guid.NewGuid().ToString(),
            Path = path,
            Content = content
        }, cancelToken);
    }

    public async Task<byte[]> ReadBytes(string path, int offset = 0, int? length = null, CancellationToken cancelToken = default)
    {
        var result = await _sender.SendCommandAsync<string>(new FileReadBytesCommand
        {
            SessionId = _sender.SessionId,
            CorrelationId = Guid.NewGuid().ToString(),
            Path = path,
            Offset = offset,
            Length = length
        }, cancelToken);

        return result != null ? Convert.FromBase64String(result) : Array.Empty<byte>();
    }

    public Task WriteBytes(string path, byte[] content, CancellationToken cancelToken = default)
    {
        return _sender.SendCommandAsync(new FileWriteBytesCommand
        {
            SessionId = _sender.SessionId,
            CorrelationId = Guid.NewGuid().ToString(),
            Path = path,
            ContentBase64 = Convert.ToBase64String(content)
        }, cancelToken);
    }

    public Task Delete(string path, CancellationToken cancelToken = default)
    {
        return _sender.SendCommandAsync(new FileDeleteCommand
        {
            SessionId = _sender.SessionId,
            CorrelationId = Guid.NewGuid().ToString(),
            Path = path
        }, cancelToken);
    }

    public async Task<bool> DirectoryExists(string path, CancellationToken cancelToken = default)
    {
        var result = await _sender.SendCommandAsync<bool>(new DirectoryExistsCommand
        {
            SessionId = _sender.SessionId,
            CorrelationId = Guid.NewGuid().ToString(),
            Path = path
        }, cancelToken);

        return result;
    }

    public Task CreateDirectory(string path, CancellationToken cancelToken = default)
    {
        return _sender.SendCommandAsync(new DirectoryCreateCommand
        {
            SessionId = _sender.SessionId,
            CorrelationId = Guid.NewGuid().ToString(),
            Path = path
        }, cancelToken);
    }

    public Task DeleteDirectory(string path, bool recursive = false, CancellationToken cancelToken = default)
    {
        return _sender.SendCommandAsync(new DirectoryDeleteCommand
        {
            SessionId = _sender.SessionId,
            CorrelationId = Guid.NewGuid().ToString(),
            Path = path,
            Recursive = recursive
        }, cancelToken);
    }

    public async Task<string[]> ListDirectory(string path, CancellationToken cancelToken = default)
    {
        var result = await _sender.SendCommandAsync<List<string>>(new DirectoryListCommand
        {
            SessionId = _sender.SessionId,
            CorrelationId = Guid.NewGuid().ToString(),
            Path = path
        }, cancelToken);

        return result?.ToArray() ?? Array.Empty<string>();
    }
}
