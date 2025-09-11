using MediatR;

namespace DbApp.Application.UserSystem.Authentication;

/// <summary>
/// Command to authenticate a user and create a session.
/// </summary>
public record LoginCommand(
    string Username,
    string Password,
    bool RememberMe,
    string? IpAddress,
    string? UserAgent
) : IRequest<LoginResponseDto>;

/// <summary>
/// Command to refresh an access token.
/// </summary>
public record RefreshTokenCommand(
    string RefreshToken
) : IRequest<RefreshTokenResponseDto>;

/// <summary>
/// Command to logout a user and invalidate their session.
/// </summary>
public record LogoutCommand(
    int UserId,
    string? AccessToken
) : IRequest<Unit>;

/// <summary>
/// Command to initiate password reset process.
/// </summary>
public record ForgotPasswordCommand(
    string UsernameOrEmail,
    string? IpAddress,
    string? UserAgent
) : IRequest<Unit>;

/// <summary>
/// Command to reset password using a reset token.
/// </summary>
public record ResetPasswordCommand(
    string Token,
    string NewPassword,
    string ConfirmPassword
) : IRequest<Unit>;

/// <summary>
/// Command to change user password.
/// </summary>
public record ChangePasswordCommand(
    int UserId,
    string CurrentPassword,
    string NewPassword,
    string ConfirmPassword
) : IRequest<Unit>;

/// <summary>
/// Command to update user profile information.
/// </summary>
public record UpdateProfileCommand(
    int UserId,
    string? DisplayName,
    string? Email,
    string? PhoneNumber,
    DateTime? BirthDate,
    int? Gender
) : IRequest<UserInfoDto>;
