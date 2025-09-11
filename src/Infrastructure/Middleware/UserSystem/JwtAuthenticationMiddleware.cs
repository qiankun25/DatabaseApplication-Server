using DbApp.Domain.Services.UserSystem;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

namespace DbApp.Infrastructure.Middleware.UserSystem;

/// <summary>
/// Middleware for JWT authentication and token blacklist validation.
/// </summary>
public class JwtAuthenticationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<JwtAuthenticationMiddleware> _logger;

    public JwtAuthenticationMiddleware(RequestDelegate next, ILogger<JwtAuthenticationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IJwtTokenService jwtTokenService, ICacheService cacheService)
    {
        try
        {
            var token = ExtractTokenFromHeader(context);
            
            if (!string.IsNullOrEmpty(token))
            {
                // Check if token is blacklisted
                var blacklistKey = $"blacklisted_token:{token}";
                var isBlacklisted = await cacheService.ExistsAsync(blacklistKey);
                
                if (isBlacklisted)
                {
                    _logger.LogWarning("Blacklisted token used: {Token}", token.Substring(0, Math.Min(10, token.Length)));
                    context.Response.StatusCode = 401;
                    await context.Response.WriteAsync("Token has been invalidated");
                    return;
                }

                // Validate token
                var principal = jwtTokenService.ValidateToken(token);
                if (principal != null)
                {
                    context.User = principal;
                    
                    // Update last active time for the session
                    var userIdClaim = principal.FindFirst(ClaimTypes.NameIdentifier);
                    if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
                    {
                        await UpdateSessionActivityAsync(cacheService, userId, token);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in JWT authentication middleware");
        }

        await _next(context);
    }

    private static string? ExtractTokenFromHeader(HttpContext context)
    {
        var authHeader = context.Request.Headers.Authorization.ToString();
        if (authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return authHeader.Substring("Bearer ".Length).Trim();
        }
        return null;
    }

    private async Task UpdateSessionActivityAsync(ICacheService cacheService, int userId, string token)
    {
        try
        {
            var sessionKey = $"user_session:{userId}";
            var session = await cacheService.GetAsync<Domain.Models.UserSystem.Authentication.UserSession>(sessionKey);
            
            if (session != null && session.AccessToken == token && session.IsActive)
            {
                session.LastActiveTime = DateTime.UtcNow;
                await cacheService.SetAsync(sessionKey, session, TimeSpan.FromDays(1));
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to update session activity for user: {UserId}", userId);
        }
    }
}
