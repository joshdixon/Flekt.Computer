namespace Flekt.Computer.Abstractions.Contracts;

public sealed record WindowGetActiveIdCommand : ComputerCommand;

public sealed record WindowGetInfoCommand : ComputerCommand
{
    public required string WindowId { get; init; }
}

public sealed record WindowActivateCommand : ComputerCommand
{
    public required string WindowId { get; init; }
}

public sealed record WindowCloseCommand : ComputerCommand
{
    public required string WindowId { get; init; }
}

public sealed record WindowMaximizeCommand : ComputerCommand
{
    public required string WindowId { get; init; }
}

public sealed record WindowMinimizeCommand : ComputerCommand
{
    public required string WindowId { get; init; }
}

public sealed record WindowRestoreCommand : ComputerCommand
{
    public required string WindowId { get; init; }
}

public sealed record WindowListCommand : ComputerCommand;

