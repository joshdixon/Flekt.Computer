namespace Flekt.Computer.Abstractions.Contracts;

public sealed record MouseLeftClickCommand : ComputerCommand
{
    public int? X { get; init; }
    public int? Y { get; init; }
}

public sealed record MouseRightClickCommand : ComputerCommand
{
    public int? X { get; init; }
    public int? Y { get; init; }
}

public sealed record MouseDoubleClickCommand : ComputerCommand
{
    public int? X { get; init; }
    public int? Y { get; init; }
}

public sealed record MouseMoveCommand : ComputerCommand
{
    public required int X { get; init; }
    public required int Y { get; init; }
}

public sealed record MouseDownCommand : ComputerCommand
{
    public int? X { get; init; }
    public int? Y { get; init; }
    public MouseButton Button { get; init; } = MouseButton.Left;
}

public sealed record MouseUpCommand : ComputerCommand
{
    public int? X { get; init; }
    public int? Y { get; init; }
    public MouseButton Button { get; init; } = MouseButton.Left;
}

public sealed record MouseScrollCommand : ComputerCommand
{
    public required int DeltaX { get; init; }
    public required int DeltaY { get; init; }
}

public sealed record MouseMovePathCommand : ComputerCommand
{
    public required IReadOnlyList<MousePathPoint> Path { get; init; }
    public MousePathOptions? Options { get; init; }
}

public sealed record MouseDragCommand : ComputerCommand
{
    public required IReadOnlyList<MousePathPoint> Path { get; init; }
    public MouseButton Button { get; init; } = MouseButton.Left;
    public MousePathOptions? Options { get; init; }
}

public sealed record MouseDragToCommand : ComputerCommand
{
    public required int X { get; init; }
    public required int Y { get; init; }
    public MouseButton Button { get; init; } = MouseButton.Left;
}

public sealed record MouseGetPositionCommand : ComputerCommand;

