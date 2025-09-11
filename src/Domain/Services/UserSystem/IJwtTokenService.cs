using DbApp.Domain.Entities.UserSystem;
using System.Security.Claims;

namespace DbApp.Domain.Services.UserSystem;

/// <summary>
/// Service interface for JWT token operations.
/// </summary>
public interface IJwtTokenService
{
    /// <summary>
    /// Generates a JWT access token for the specified user.
    /// </summary>
    /// <param name="user">User entity</param>
    /// <returns>JWT access token</returns>
    string GenerateAccessToken(User user);

    /// <summary>
    /// Generates a refresh token.
    /// </summary>
    /// <returns>Refresh token</returns>
    string GenerateRefreshToken();

    /// <summary>
    /// Validates a JWT token and returns the claims principal.
    /// </summary>
    /// <param name="token">JWT token to validate</param>
    /// <returns>Claims principal if valid, null otherwise</returns>
    ClaimsPrincipal? ValidateToken(string token);

    /// <summary>
    /// Extracts user ID from JWT token.
    /// </summary>
    /// <param name="token">JWT token</param>
    /// <returns>User ID if valid, null otherwise</returns>
    int? GetUserIdFromToken(string token);

    /// <summary>
    /// Checks if a token is expired.
    /// </summary>
    /// <param name="token">JWT token</param>
    /// <returns>True if expired</returns>
    bool IsTokenExpired(string token);
}
