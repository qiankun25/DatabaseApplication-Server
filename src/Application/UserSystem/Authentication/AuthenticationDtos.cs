using DbApp.Domain.Enums.UserSystem;
using DbApp.Domain.Models.UserSystem.Authentication;

namespace DbApp.Application.UserSystem.Authentication;

/// <summary>
/// DTO for login request.
/// </summary>
public class LoginRequestDto
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public bool RememberMe { get; set; } = false;
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
}

/// <summary>
/// DTO for login response.
/// </summary>
public class LoginResponseDto
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public UserInfoDto User { get; set; } = new();
}

/// <summary>
/// DTO for user information.
/// </summary>
public class UserInfoDto
{
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public string RoleName { get; set; } = string.Empty;
    public int PermissionLevel { get; set; }
}

/// <summary>
/// DTO for refresh token request.
/// </summary>
public class RefreshTokenRequestDto
{
    public string RefreshToken { get; set; } = string.Empty;
}

/// <summary>
/// DTO for refresh token response.
/// </summary>
public class RefreshTokenResponseDto
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
}

/// <summary>
/// DTO for forgot password request.
/// </summary>
public class ForgotPasswordRequestDto
{
    public string UsernameOrEmail { get; set; } = string.Empty;
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
}

/// <summary>
/// DTO for reset password request.
/// </summary>
public class ResetPasswordRequestDto
{
    public string Token { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
    public string ConfirmPassword { get; set; } = string.Empty;
}

/// <summary>
/// DTO for change password request.
/// </summary>
public class ChangePasswordRequestDto
{
    public string CurrentPassword { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
    public string ConfirmPassword { get; set; } = string.Empty;
}

/// <summary>
/// DTO for update profile request.
/// </summary>
public class UpdateProfileRequestDto
{
    public string? DisplayName { get; set; }
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public DateTime? BirthDate { get; set; }
    public int? Gender { get; set; }
}

/// <summary>
/// DTO for login history entry.
/// </summary>
public class LoginHistoryDto
{
    public DateTime LoginTime { get; set; }
    public DateTime? LogoutTime { get; set; }
    public string? IpAddress { get; set; }
    public string? DeviceInfo { get; set; }
    public string? Location { get; set; }
    public bool IsSuccessful { get; set; }
    public string? FailureReason { get; set; }
    public int? SessionDurationMinutes { get; set; }
    public bool IsActiveSession { get; set; }
}

/// <summary>
/// DTO for user session information.
/// </summary>
public class UserSessionDto
{
    public DateTime LoginTime { get; set; }
    public DateTime LastActiveTime { get; set; }
    public string? IpAddress { get; set; }
    public string? DeviceInfo { get; set; }
    public bool IsActive { get; set; }
    public DateTime ExpiresAt { get; set; }
}
