namespace DbApp.Domain.Models.UserSystem.Authentication;

/// <summary>
/// Represents login failure information stored in cache.
/// Used for tracking failed login attempts and account lockout.
/// </summary>
public class LoginFailureInfo
{
    /// <summary>
    /// Username or identifier for the failed login attempts.
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Number of consecutive failed login attempts.
    /// </summary>
    public int FailureCount { get; set; } = 0;

    /// <summary>
    /// Time of the first failed login attempt in the current sequence.
    /// </summary>
    public DateTime FirstFailureTime { get; set; }

    /// <summary>
    /// Time of the most recent failed login attempt.
    /// </summary>
    public DateTime LastFailureTime { get; set; }

    /// <summary>
    /// Time until which the account is locked (if applicable).
    /// </summary>
    public DateTime? LockoutUntil { get; set; }

    /// <summary>
    /// Whether the account is currently locked.
    /// </summary>
    public bool IsLocked => LockoutUntil.HasValue && LockoutUntil.Value > DateTime.UtcNow;

    /// <summary>
    /// IP addresses from which failed login attempts were made.
    /// </summary>
    public List<string> FailureIpAddresses { get; set; } = new();

    /// <summary>
    /// Resets the failure information after a successful login.
    /// </summary>
    public void Reset()
    {
        FailureCount = 0;
        LockoutUntil = null;
        FailureIpAddresses.Clear();
    }

    /// <summary>
    /// Records a new login failure.
    /// </summary>
    /// <param name="ipAddress">IP address of the failed attempt</param>
    /// <param name="maxAttempts">Maximum allowed attempts before lockout</param>
    /// <param name="lockoutDuration">Duration of lockout</param>
    public void RecordFailure(string? ipAddress, int maxAttempts = 5, TimeSpan? lockoutDuration = null)
    {
        if (FailureCount == 0)
        {
            FirstFailureTime = DateTime.UtcNow;
        }

        FailureCount++;
        LastFailureTime = DateTime.UtcNow;

        if (!string.IsNullOrEmpty(ipAddress) && !FailureIpAddresses.Contains(ipAddress))
        {
            FailureIpAddresses.Add(ipAddress);
        }

        if (FailureCount >= maxAttempts)
        {
            LockoutUntil = DateTime.UtcNow.Add(lockoutDuration ?? TimeSpan.FromMinutes(30));
        }
    }
}
