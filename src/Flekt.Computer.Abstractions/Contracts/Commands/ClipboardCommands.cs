namespace Flekt.Computer.Abstractions.Contracts;

// Text clipboard (existing)
public sealed record ClipboardGetCommand : ComputerCommand;

public sealed record ClipboardSetCommand : ComputerCommand
{
    public required string Text { get; init; }
}

// File clipboard
public sealed record ClipboardSetFilesCommand : ComputerCommand
{
    /// <summary>
    /// URLs to download and set as clipboard files (CF_HDROP).
    /// </summary>
    public required string[] Urls { get; init; }
}

public sealed record ClipboardSetFilesFromPathsCommand : ComputerCommand
{
    /// <summary>
    /// Local file paths to set on clipboard (CF_HDROP).
    /// </summary>
    public required string[] Paths { get; init; }
}

public sealed record ClipboardGetFilesCommand : ComputerCommand;

// Image clipboard
public sealed record ClipboardSetImageFromUrlCommand : ComputerCommand
{
    public required string Url { get; init; }
}

public sealed record ClipboardSetImageFromBytesCommand : ComputerCommand
{
    /// <summary>
    /// Base64-encoded image data.
    /// </summary>
    public required string ImageBase64 { get; init; }
}

public sealed record ClipboardGetImageCommand : ComputerCommand;

// Content type
public sealed record ClipboardGetContentTypeCommand : ComputerCommand;

