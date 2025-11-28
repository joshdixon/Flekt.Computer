namespace Flekt.Computer.Abstractions.Contracts;

public sealed record ClipboardGetCommand : ComputerCommand;

public sealed record ClipboardSetCommand : ComputerCommand
{
    public required string Text { get; init; }
}

