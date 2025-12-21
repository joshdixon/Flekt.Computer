using Flekt.Computer.Abstractions;

namespace Flekt.Computer.Interface;

/// <summary>
/// Implementation of IComputerInterface that routes commands through the cloud provider.
/// </summary>
internal sealed class ComputerInterface : IComputerInterface
{
    public ComputerInterface(ICommandSender commandSender)
    {
        Mouse = new CloudMouse(commandSender);
        Keyboard = new CloudKeyboard(commandSender);
        Screen = new CloudScreen(commandSender);
        Clipboard = new CloudClipboard(commandSender);
        Files = new CloudFiles(commandSender);
        Shell = new CloudShell(commandSender);
        Windows = new CloudWindows(commandSender);
    }

    public IMouse Mouse { get; }
    public IKeyboard Keyboard { get; }
    public IScreen Screen { get; }
    public IClipboard Clipboard { get; }
    public IFiles Files { get; }
    public IShell Shell { get; }
    public IWindows Windows { get; }
}










