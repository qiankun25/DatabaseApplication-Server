namespace DbApp.Domain.Models.UserSystem.Authentication;

/// <summary>
/// Represents a user session stored in cache.
/// Contains all session-related information for authenticated users.
/// </summary>
public class UserSession
{
    /// <summary>
    /// User ID associated with this session.
    /// </summary>
    public int UserId { get; set; }

    /// <summary>
    /// Username for this session.
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// JWT access token.
    /// </summary>
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>
    /// JWT refresh token.
    /// </summary>
    public string RefreshToken { get; set; } = string.Empty;

    /// <summary>
    /// When the session was created (login time).
    /// </summary>
    public DateTime LoginTime { get; set; }

    /// <summary>
    /// Last activity time for this session.
    /// </summary>
    public DateTime LastActiveTime { get; set; }

    /// <summary>
    /// IP address from which the user logged in.
    /// </summary>
    public string? IpAddress { get; set; }

    /// <summary>
    /// User agent string from the login request.
    /// </summary>
    public string? UserAgent { get; set; }

    /// <summary>
    /// Device information for this session.
    /// </summary>
    public string? DeviceInfo { get; set; }

    /// <summary>
    /// Whether this session is still active.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Session expiration time.
    /// </summary>
    public DateTime ExpiresAt { get; set; }
}
