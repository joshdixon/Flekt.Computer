using System.Text.Json.Serialization;

namespace Flekt.Computer.Abstractions.Contracts;

/// <summary>
/// Base class for all computer commands.
/// Uses polymorphic JSON serialization for type safety.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
// Mouse commands
[JsonDerivedType(typeof(MouseLeftClickCommand), "mouse.leftClick")]
[JsonDerivedType(typeof(MouseRightClickCommand), "mouse.rightClick")]
[JsonDerivedType(typeof(MouseDoubleClickCommand), "mouse.doubleClick")]
[JsonDerivedType(typeof(MouseMoveCommand), "mouse.move")]
[JsonDerivedType(typeof(MouseDownCommand), "mouse.down")]
[JsonDerivedType(typeof(MouseUpCommand), "mouse.up")]
[JsonDerivedType(typeof(MouseScrollCommand), "mouse.scroll")]
[JsonDerivedType(typeof(MouseMovePathCommand), "mouse.movePath")]
[JsonDerivedType(typeof(MouseDragCommand), "mouse.drag")]
[JsonDerivedType(typeof(MouseDragToCommand), "mouse.dragTo")]
[JsonDerivedType(typeof(MouseGetPositionCommand), "mouse.getPosition")]
// Keyboard commands
[JsonDerivedType(typeof(KeyboardTypeCommand), "keyboard.type")]
[JsonDerivedType(typeof(KeyboardPressCommand), "keyboard.press")]
[JsonDerivedType(typeof(KeyboardHotkeyCommand), "keyboard.hotkey")]
[JsonDerivedType(typeof(KeyboardDownCommand), "keyboard.down")]
[JsonDerivedType(typeof(KeyboardUpCommand), "keyboard.up")]
// Screen commands
[JsonDerivedType(typeof(ScreenScreenshotCommand), "screen.screenshot")]
[JsonDerivedType(typeof(ScreenGetSizeCommand), "screen.getSize")]
// Clipboard commands
[JsonDerivedType(typeof(ClipboardGetCommand), "clipboard.get")]
[JsonDerivedType(typeof(ClipboardSetCommand), "clipboard.set")]
[JsonDerivedType(typeof(ClipboardSetFilesCommand), "clipboard.setFiles")]
[JsonDerivedType(typeof(ClipboardSetFilesFromPathsCommand), "clipboard.setFilesFromPaths")]
[JsonDerivedType(typeof(ClipboardGetFilesCommand), "clipboard.getFiles")]
[JsonDerivedType(typeof(ClipboardSetImageFromUrlCommand), "clipboard.setImageFromUrl")]
[JsonDerivedType(typeof(ClipboardSetImageFromBytesCommand), "clipboard.setImageFromBytes")]
[JsonDerivedType(typeof(ClipboardGetImageCommand), "clipboard.getImage")]
[JsonDerivedType(typeof(ClipboardGetContentTypeCommand), "clipboard.getContentType")]
// Shell commands
[JsonDerivedType(typeof(ShellRunCommand), "shell.run")]
// File commands
[JsonDerivedType(typeof(FileExistsCommand), "files.exists")]
[JsonDerivedType(typeof(FileReadTextCommand), "files.readText")]
[JsonDerivedType(typeof(FileWriteTextCommand), "files.writeText")]
[JsonDerivedType(typeof(FileReadBytesCommand), "files.readBytes")]
[JsonDerivedType(typeof(FileWriteBytesCommand), "files.writeBytes")]
[JsonDerivedType(typeof(FileDeleteCommand), "files.delete")]
[JsonDerivedType(typeof(DirectoryExistsCommand), "files.directoryExists")]
[JsonDerivedType(typeof(DirectoryCreateCommand), "files.createDirectory")]
[JsonDerivedType(typeof(DirectoryDeleteCommand), "files.deleteDirectory")]
[JsonDerivedType(typeof(DirectoryListCommand), "files.listDirectory")]
// Window commands
[JsonDerivedType(typeof(WindowGetActiveIdCommand), "windows.getActiveId")]
[JsonDerivedType(typeof(WindowGetInfoCommand), "windows.getInfo")]
[JsonDerivedType(typeof(WindowActivateCommand), "windows.activate")]
[JsonDerivedType(typeof(WindowCloseCommand), "windows.close")]
[JsonDerivedType(typeof(WindowMaximizeCommand), "windows.maximize")]
[JsonDerivedType(typeof(WindowMinimizeCommand), "windows.minimize")]
[JsonDerivedType(typeof(WindowRestoreCommand), "windows.restore")]
[JsonDerivedType(typeof(WindowListCommand), "windows.list")]
public abstract record ComputerCommand
{
    /// <summary>
    /// The session ID this command belongs to.
    /// </summary>
    public required string SessionId { get; init; }

    /// <summary>
    /// Unique correlation ID for matching responses.
    /// </summary>
    public required string CorrelationId { get; init; }

    /// <summary>
    /// When the command was created.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}
