namespace Flekt.Computer.Abstractions;

/// <summary>
/// Public API response with RDP connection info.
/// Gateway-based access using RD Gateway for secure routing to VMs.
/// </summary>
public sealed record RdpAccessInfo
{
    /// <summary>
    /// RD Gateway hostname.
    /// </summary>
    public required string Gateway { get; init; }

    /// <summary>
    /// Resource name (session ID) - used by gateway for routing.
    /// </summary>
    public required string Resource { get; init; }

    /// <summary>
    /// Temporary username for this session.
    /// </summary>
    public required string Username { get; init; }

    /// <summary>
    /// Temporary password (auto-generated).
    /// </summary>
    public required string Password { get; init; }

    /// <summary>
    /// When the RDP access expires.
    /// </summary>
    public required DateTimeOffset ExpiresAt { get; init; }

    /// <summary>
    /// Pre-built .rdp file content for one-click connect.
    /// </summary>
    public required string RdpFileContent { get; init; }
    
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

