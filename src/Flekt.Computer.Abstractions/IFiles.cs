namespace Flekt.Computer.Abstractions;

/// <summary>
/// File and directory operations.
/// </summary>
public interface IFiles
{
    /// <summary>
    /// Checks if a file exists at the specified path.
    /// </summary>
    Task<bool> Exists(string path, CancellationToken cancelToken = default);
    
    /// <summary>
    /// Reads the text content of a file.
    /// </summary>
    Task<string> ReadText(string path, CancellationToken cancelToken = default);
    
    /// <summary>
    /// Writes text content to a file.
    /// </summary>
    Task WriteText(string path, string content, CancellationToken cancelToken = default);
    
    /// <summary>
    /// Reads binary content from a file.
    /// </summary>
    Task<byte[]> ReadBytes(string path, int offset = 0, int? length = null, CancellationToken cancelToken = default);
    
    /// <summary>
    /// Writes binary content to a file.
    /// </summary>
    Task WriteBytes(string path, byte[] content, CancellationToken cancelToken = default);
    
    /// <summary>
    /// Deletes a file.
    /// </summary>
    Task Delete(string path, CancellationToken cancelToken = default);
    
    /// <summary>
    /// Checks if a directory exists.
    /// </summary>
    Task<bool> DirectoryExists(string path, CancellationToken cancelToken = default);
    
    /// <summary>
    /// Creates a directory.
    /// </summary>
    Task CreateDirectory(string path, CancellationToken cancelToken = default);
    
    /// <summary>
    /// Deletes a directory.
    /// </summary>
    Task DeleteDirectory(string path, bool recursive = false, CancellationToken cancelToken = default);
    
    /// <summary>
    /// Lists the contents of a directory.
    /// </summary>
    Task<string[]> ListDirectory(string path, CancellationToken cancelToken = default);
}

