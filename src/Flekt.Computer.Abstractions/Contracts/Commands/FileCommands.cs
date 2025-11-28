namespace Flekt.Computer.Abstractions.Contracts;

public sealed record FileExistsCommand : ComputerCommand
{
    public required string Path { get; init; }
}

public sealed record FileReadTextCommand : ComputerCommand
{
    public required string Path { get; init; }
}

public sealed record FileWriteTextCommand : ComputerCommand
{
    public required string Path { get; init; }
    public required string Content { get; init; }
}

public sealed record FileReadBytesCommand : ComputerCommand
{
    public required string Path { get; init; }
    public int Offset { get; init; }
    public int? Length { get; init; }
}

public sealed record FileWriteBytesCommand : ComputerCommand
{
    public required string Path { get; init; }
    /// <summary>
    /// Base64-encoded byte content.
    /// </summary>
    public required string ContentBase64 { get; init; }
}

public sealed record FileDeleteCommand : ComputerCommand
{
    public required string Path { get; init; }
}

public sealed record DirectoryExistsCommand : ComputerCommand
{
    public required string Path { get; init; }
}

public sealed record DirectoryCreateCommand : ComputerCommand
{
    public required string Path { get; init; }
}

public sealed record DirectoryDeleteCommand : ComputerCommand
{
    public required string Path { get; init; }
    public bool Recursive { get; init; }
}

public sealed record DirectoryListCommand : ComputerCommand
{
    public required string Path { get; init; }
}

