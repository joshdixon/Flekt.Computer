namespace Flekt.Computer.Abstractions.Contracts;

public sealed record KeyboardTypeCommand : ComputerCommand
{
    public required string Text { get; init; }
}

public sealed record KeyboardPressCommand : ComputerCommand
{
    public required string Key { get; init; }
}

public sealed record KeyboardHotkeyCommand : ComputerCommand
{
    public required IReadOnlyList<string> Keys { get; init; }
}

public sealed record KeyboardDownCommand : ComputerCommand
{
    public required string Key { get; init; }
}

public sealed record KeyboardUpCommand : ComputerCommand
{
    public required string Key { get; init; }
}

