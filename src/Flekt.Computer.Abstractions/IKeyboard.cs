namespace Flekt.Computer.Abstractions;

/// <summary>
/// Keyboard operations including typing, key presses, and hotkeys.
/// </summary>
public interface IKeyboard
{
    /// <summary>
    /// Types the specified text string.
    /// </summary>
    Task Type(string text, CancellationToken cancelToken = default);
    
    /// <summary>
    /// Presses and releases a single key.
    /// </summary>
    Task Press(string key, CancellationToken cancelToken = default);
    
    /// <summary>
    /// Presses a combination of keys (hotkey/shortcut).
    /// </summary>
    Task Hotkey(CancellationToken cancelToken = default, params string[] keys);
    
    /// <summary>
    /// Presses and holds a key.
    /// </summary>
    Task Down(string key, CancellationToken cancelToken = default);
    
    /// <summary>
    /// Releases a held key.
    /// </summary>
    Task Up(string key, CancellationToken cancelToken = default);
}

