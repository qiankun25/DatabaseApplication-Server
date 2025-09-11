namespace DbApp.Domain.Models.UserSystem.Authentication;

/// <summary>
/// Represents a password reset token stored in cache.
/// Used for secure password reset functionality.
/// </summary>
public class PasswordResetToken
{
    /// <summary>
    /// User ID for whom the reset token was generated.
    /// </summary>
    public int UserId { get; set; }

    /// <summary>
    /// Username associated with the reset token.
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Email address where the reset link was sent.
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Phone number where the reset code was sent (if applicable).
    /// </summary>
    public string? PhoneNumber { get; set; }

    /// <summary>
    /// The actual reset token value.
    /// </summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>
    /// When the reset token was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// When the reset token expires.
    /// </summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// Whether the token has been used.
    /// </summary>
    public bool IsUsed { get; set; } = false;

    /// <summary>
    /// IP address from which the reset was requested.
    /// </summary>
    public string? RequestIpAddress { get; set; }

    /// <summary>
    /// User agent from the reset request.
    /// </summary>
    public string? RequestUserAgent { get; set; }

    /// <summary>
    /// Reset method used (email, phone, etc.).
    /// </summary>
    public string ResetMethod { get; set; } = "email";

    /// <summary>
    /// Checks if the token is valid and not expired.
    /// </summary>
    public bool IsValid => !IsUsed && ExpiresAt > DateTime.UtcNow;

    /// <summary>
    /// Marks the token as used.
    /// </summary>
    public void MarkAsUsed()
    {
        IsUsed = true;
    }
}
