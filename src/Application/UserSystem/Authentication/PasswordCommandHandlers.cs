using AutoMapper;
using DbApp.Application.Common.Exceptions;
using DbApp.Application.Common.Interfaces;
using DbApp.Domain.Entities.UserSystem;
using DbApp.Domain.Models.UserSystem.Authentication;
using DbApp.Domain.Services.UserSystem;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DbApp.Application.UserSystem.Authentication;

/// <summary>
/// Handler for forgot password command.
/// </summary>
public class ForgotPasswordCommandHandler : IRequestHandler<ForgotPasswordCommand, Unit>
{
    private readonly IApplicationDbContext _context;
    private readonly ICacheService _cacheService;
    private readonly ILogger<ForgotPasswordCommandHandler> _logger;

    public ForgotPasswordCommandHandler(
        IApplicationDbContext context,
        ICacheService cacheService,
        ILogger<ForgotPasswordCommandHandler> logger)
    {
        _context = context;
        _cacheService = cacheService;
        _logger = logger;
    }

    public async Task<Unit> Handle(ForgotPasswordCommand request, CancellationToken cancellationToken)
    {
        try
        {
            // Find user by username or email
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Username == request.UsernameOrEmail || u.Email == request.UsernameOrEmail, 
                    cancellationToken);

            if (user == null)
            {
                _logger.LogWarning("Password reset requested for non-existent user: {UsernameOrEmail}", request.UsernameOrEmail);
                // Don't reveal that user doesn't exist for security reasons
                return Unit.Value;
            }

            if (string.IsNullOrEmpty(user.Email))
            {
                _logger.LogWarning("Password reset requested for user without email: {Username}", user.Username);
                throw new ValidationException("No email address associated with this account.");
            }

            // Generate reset token
            var resetToken = Guid.NewGuid().ToString("N");
            var passwordResetToken = new PasswordResetToken
            {
                UserId = user.UserId,
                Username = user.Username,
                Email = user.Email,
                Token = resetToken,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddHours(1), // 1 hour expiration
                RequestIpAddress = request.IpAddress,
                RequestUserAgent = request.UserAgent,
                ResetMethod = "email"
            };

            // Store reset token in cache
            var tokenKey = $"password_reset_token:{resetToken}";
            await _cacheService.SetAsync(tokenKey, passwordResetToken, TimeSpan.FromHours(1));

            // Also store by user ID for tracking
            var userResetKey = $"user_password_reset:{user.UserId}";
            await _cacheService.SetAsync(userResetKey, new { Token = resetToken, CreatedAt = DateTime.UtcNow }, 
                TimeSpan.FromHours(1));

            _logger.LogInformation("Password reset token generated for user: {Username}", user.Username);

            // TODO: Send email with reset link
            // For now, we'll just log the token (in production, this should be sent via email)
            _logger.LogInformation("Password reset token for {Username}: {Token}", user.Username, resetToken);

            return Unit.Value;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating password reset token for: {UsernameOrEmail}", request.UsernameOrEmail);
            throw;
        }
    }
}

/// <summary>
/// Handler for reset password command.
/// </summary>
public class ResetPasswordCommandHandler : IRequestHandler<ResetPasswordCommand, Unit>
{
    private readonly IApplicationDbContext _context;
    private readonly ICacheService _cacheService;
    private readonly IAuthenticationService _authService;
    private readonly ILogger<ResetPasswordCommandHandler> _logger;

    public ResetPasswordCommandHandler(
        IApplicationDbContext context,
        ICacheService cacheService,
        IAuthenticationService authService,
        ILogger<ResetPasswordCommandHandler> logger)
    {
        _context = context;
        _cacheService = cacheService;
        _authService = authService;
        _logger = logger;
    }

    public async Task<Unit> Handle(ResetPasswordCommand request, CancellationToken cancellationToken)
    {
        try
        {
            // Validate passwords match
            if (request.NewPassword != request.ConfirmPassword)
            {
                throw new ValidationException("Passwords do not match.");
            }

            // Validate password strength
            if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 6)
            {
                throw new ValidationException("Password must be at least 6 characters long.");
            }

            // Get reset token from cache
            var tokenKey = $"password_reset_token:{request.Token}";
            var resetToken = await _cacheService.GetAsync<PasswordResetToken>(tokenKey);

            if (resetToken == null || !resetToken.IsValid)
            {
                _logger.LogWarning("Invalid or expired password reset token used: {Token}", request.Token);
                throw new ValidationException("Invalid or expired reset token.");
            }

            // Get user
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == resetToken.UserId, cancellationToken);
            if (user == null)
            {
                _logger.LogWarning("User not found for password reset: {UserId}", resetToken.UserId);
                throw new ValidationException("User not found.");
            }

            // Hash new password and update user
            var hashedPassword = _authService.HashPassword(request.NewPassword);
            user.PasswordHash = hashedPassword;
            user.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync(cancellationToken);

            // Mark token as used and remove from cache
            resetToken.MarkAsUsed();
            await _cacheService.RemoveAsync(tokenKey);
            
            // Remove user reset tracking
            var userResetKey = $"user_password_reset:{resetToken.UserId}";
            await _cacheService.RemoveAsync(userResetKey);

            // Invalidate all existing sessions for this user
            await InvalidateUserSessionsAsync(resetToken.UserId);

            _logger.LogInformation("Password reset completed for user: {Username}", user.Username);

            return Unit.Value;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resetting password with token: {Token}", request.Token);
            throw;
        }
    }

    private async Task InvalidateUserSessionsAsync(int userId)
    {
        var sessionKey = $"user_session:{userId}";
        var session = await _cacheService.GetAsync<UserSession>(sessionKey);
        
        if (session != null)
        {
            // Add current tokens to blacklist
            if (!string.IsNullOrEmpty(session.AccessToken))
            {
                var blacklistKey = $"blacklisted_token:{session.AccessToken}";
                await _cacheService.SetAsync(blacklistKey, new { UserId = userId, BlacklistedAt = DateTime.UtcNow }, 
                    TimeSpan.FromHours(24));
            }

            if (!string.IsNullOrEmpty(session.RefreshToken))
            {
                var refreshTokenKey = $"refresh_token:{session.RefreshToken}";
                await _cacheService.RemoveAsync(refreshTokenKey);
            }

            // Remove session
            await _cacheService.RemoveAsync(sessionKey);
        }
    }
}

/// <summary>
/// Handler for change password command.
/// </summary>
public class ChangePasswordCommandHandler : IRequestHandler<ChangePasswordCommand, Unit>
{
    private readonly IApplicationDbContext _context;
    private readonly IAuthenticationService _authService;
    private readonly ICacheService _cacheService;
    private readonly ILogger<ChangePasswordCommandHandler> _logger;

    public ChangePasswordCommandHandler(
        IApplicationDbContext context,
        IAuthenticationService authService,
        ICacheService cacheService,
        ILogger<ChangePasswordCommandHandler> logger)
    {
        _context = context;
        _authService = authService;
        _cacheService = cacheService;
        _logger = logger;
    }

    public async Task<Unit> Handle(ChangePasswordCommand request, CancellationToken cancellationToken)
    {
        try
        {
            // Validate passwords match
            if (request.NewPassword != request.ConfirmPassword)
            {
                throw new ValidationException("Passwords do not match.");
            }

            // Validate password strength
            if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 6)
            {
                throw new ValidationException("Password must be at least 6 characters long.");
            }

            // Get user
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == request.UserId, cancellationToken);
            if (user == null)
            {
                throw new ValidationException("User not found.");
            }

            // Verify current password
            if (!_authService.VerifyPassword(request.CurrentPassword, user.PasswordHash))
            {
                _logger.LogWarning("Invalid current password provided for user: {UserId}", request.UserId);
                throw new ValidationException("Current password is incorrect.");
            }

            // Hash new password and update user
            var hashedPassword = _authService.HashPassword(request.NewPassword);
            user.PasswordHash = hashedPassword;
            user.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Password changed for user: {UserId}", request.UserId);

            return Unit.Value;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error changing password for user: {UserId}", request.UserId);
            throw;
        }
    }
}
