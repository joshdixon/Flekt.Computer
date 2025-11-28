namespace Flekt.Computer.Abstractions;

/// <summary>
/// Result of executing a shell command on the remote computer.
/// </summary>
public sealed record CommandResult
{
    /// <summary>
    /// The exit code of the command.
    /// </summary>
    public required int ExitCode { get; init; }
    
    /// <summary>
    /// The standard output of the command.
    /// </summary>
    public required string StandardOutput { get; init; }
    
    /// <summary>
    /// The standard error output of the command.
    /// </summary>
    public required string StandardError { get; init; }
    
    /// <summary>
    /// Whether the command executed successfully (exit code 0).
    /// </summary>
    public bool Success => ExitCode == 0;
    
    /// <summary>
    /// The duration of the command execution.
    /// </summary>
    public TimeSpan? Duration { get; init; }
    
    /// <summary>
    /// Creates a successful result with the given output.
    /// </summary>
    public static CommandResult Ok(string output = "", string error = "") => new()
    {
        ExitCode = 0,
        StandardOutput = output,
        StandardError = error
    };
    
    /// <summary>
    /// Creates a failed result with the given exit code and error.
    /// </summary>
    public static CommandResult Fail(int exitCode, string error = "", string output = "") => new()
    {
        ExitCode = exitCode,
        StandardOutput = output,
        StandardError = error
    };
}

