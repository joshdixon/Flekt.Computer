namespace Flekt.Computer.Abstractions.Contracts;

public sealed record ShellRunCommand : ComputerCommand
{
    public required string Command { get; init; }
    public TimeSpan? Timeout { get; init; }
}

