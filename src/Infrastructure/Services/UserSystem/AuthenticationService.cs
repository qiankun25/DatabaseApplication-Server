using DbApp.Application.Common.Interfaces;
using DbApp.Domain.Entities.UserSystem;
using DbApp.Domain.Services.UserSystem;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DbApp.Infrastructure.Services.UserSystem;

/// <summary>
/// Implementation of authentication service for user credential validation.
/// </summary>
public class AuthenticationService : IAuthenticationService
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<AuthenticationService> _logger;

    public AuthenticationService(IApplicationDbContext context, ILogger<AuthenticationService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<User?> ValidateCredentialsAsync(string username, string password)
    {
        try
        {
            // 检查用户名和密码是否为空
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                return null;
            }

            var user = await _context.Users
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.Username == username);

            if (user == null)
            {
                _logger.LogWarning("User not found: {Username}", username);
                return null;
            }

            if (!VerifyPassword(password, user.PasswordHash))
            {
                _logger.LogWarning("Invalid password for user: {Username}", username);
                return null;
            }

            return user;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating credentials for user: {Username}", username);
            return null;
        }
    }

    public bool VerifyPassword(string password, string hash)
    {
        try
        {
            return BCrypt.Net.BCrypt.Verify(password, hash);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying password");
            return false;
        }
    }

    public string HashPassword(string password)
    {
        try
        {
            return BCrypt.Net.BCrypt.HashPassword(password, BCrypt.Net.BCrypt.GenerateSalt(12));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error hashing password");
            throw;
        }
    }
}
