namespace DbApp.Domain.Models.UserSystem.Authentication;

/// <summary>
/// Represents a single login history entry stored in cache.
/// Used for tracking user login activities.
/// </summary>
public class LoginHistoryEntry
{
    /// <summary>
    /// User ID for this login entry.
    /// </summary>
    public int UserId { get; set; }

    /// <summary>
    /// Username for this login entry.
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Login timestamp.
    /// </summary>
    public DateTime LoginTime { get; set; }

    /// <summary>
    /// Logout timestamp (if applicable).
    /// </summary>
    public DateTime? LogoutTime { get; set; }

    /// <summary>
    /// IP address from which the login occurred.
    /// </summary>
    public string? IpAddress { get; set; }

    /// <summary>
    /// User agent string from the login request.
    /// </summary>
    public string? UserAgent { get; set; }

    /// <summary>
    /// Device information extracted from user agent.
    /// </summary>
    public string? DeviceInfo { get; set; }

    /// <summary>
    /// Location information based on IP address (if available).
    /// </summary>
    public string? Location { get; set; }

    /// <summary>
    /// Whether the login was successful.
    /// </summary>
    public bool IsSuccessful { get; set; } = true;

    /// <summary>
    /// Reason for login failure (if applicable).
    /// </summary>
    public string? FailureReason { get; set; }

    /// <summary>
    /// Session duration in minutes.
    /// </summary>
    public int? SessionDurationMinutes => LogoutTime.HasValue 
        ? (int)(LogoutTime.Value - LoginTime).TotalMinutes 
        : null;

    /// <summary>
    /// Whether this is an active session (no logout time).
    /// </summary>
    public bool IsActiveSession => !LogoutTime.HasValue && IsSuccessful;
}
