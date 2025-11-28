namespace Flekt.Computer.Abstractions;

/// <summary>
/// Information for connecting to a computer via RDP.
/// </summary>
public sealed record RdpAccessInfo
{
    /// <summary>
    /// The server address (gateway or direct host).
    /// </summary>
    public required string Server { get; init; }
    
    /// <summary>
    /// The port to connect to. Null means default (3389).
    /// </summary>
    public int? Port { get; init; }
    
    /// <summary>
    /// The username for authentication.
    /// </summary>
    public required string Username { get; init; }
    
    /// <summary>
    /// The password for authentication.
    /// </summary>
    public required string Password { get; init; }
    
    /// <summary>
    /// When these credentials expire.
    /// </summary>
    public required DateTimeOffset ExpiresAt { get; init; }
    
    /// <summary>
    /// Pre-generated .rdp file content for one-click connection.
    /// </summary>
    public string? RdpFileContent { get; init; }
    
    /// <summary>
    /// Gets the full connection string.
    /// </summary>
    public string ConnectionString => Port.HasValue 
        ? $"{Server}:{Port.Value}" 
        : Server;
    
    /// <summary>
    /// Whether the credentials have expired.
    /// </summary>
    public bool IsExpired => DateTimeOffset.UtcNow >= ExpiresAt;
    
    /// <summary>
    /// Time remaining until expiration.
    /// </summary>
    public TimeSpan TimeRemaining => IsExpired 
        ? TimeSpan.Zero 
        : ExpiresAt - DateTimeOffset.UtcNow;
}

