namespace Flekt.Computer.Abstractions;

/// <summary>
/// Shell command execution.
/// </summary>
public interface IShell
{
    /// <summary>
    /// Executes a shell command and returns the result.
    /// </summary>
    Task<CommandResult> Run(string command, CancellationToken cancelToken = default);
    
    /// <summary>
    /// Executes a shell command with the specified timeout.
    /// </summary>
    Task<CommandResult> Run(string command, TimeSpan timeout, CancellationToken cancelToken = default);
}

