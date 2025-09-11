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
/// Handler for login command.
/// </summary>
public class LoginCommandHandler : IRequestHandler<LoginCommand, LoginResponseDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IAuthenticationService _authService;
    private readonly ICacheService _cacheService;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IMapper _mapper;
    private readonly ILogger<LoginCommandHandler> _logger;

    public LoginCommandHandler(
        IApplicationDbContext context,
        IAuthenticationService authService,
        ICacheService cacheService,
        IJwtTokenService jwtTokenService,
        IMapper mapper,
        ILogger<LoginCommandHandler> logger)
    {
        _context = context;
        _authService = authService;
        _cacheService = cacheService;
        _jwtTokenService = jwtTokenService;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<LoginResponseDto> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        try
        {
            // Check for account lockout
            var lockoutKey = $"login_failures:{request.Username.ToLower()}";
            var failureInfo = await _cacheService.GetAsync<LoginFailureInfo>(lockoutKey);
            
            if (failureInfo?.IsLocked == true)
            {
                _logger.LogWarning("Login attempt for locked account: {Username}", request.Username);
                throw new ValidationException("Account is temporarily locked due to multiple failed login attempts.");
            }

            // Validate credentials
            var user = await _authService.ValidateCredentialsAsync(request.Username, request.Password);
            if (user == null)
            {
                await RecordLoginFailureAsync(request.Username, request.IpAddress);
                _logger.LogWarning("Failed login attempt for username: {Username}", request.Username);
                throw new ValidationException("Invalid username or password.");
            }

            // Clear any existing failure records
            await _cacheService.RemoveAsync(lockoutKey);

            // Generate tokens
            var accessToken = _jwtTokenService.GenerateAccessToken(user);
            var refreshToken = _jwtTokenService.GenerateRefreshToken();
            var expiresAt = DateTime.UtcNow.AddHours(request.RememberMe ? 24 * 7 : 24); // 7 days if remember me, 1 day otherwise

            // Create session
            var session = new UserSession
            {
                UserId = user.UserId,
                Username = user.Username,
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                LoginTime = DateTime.UtcNow,
                LastActiveTime = DateTime.UtcNow,
                IpAddress = request.IpAddress,
                UserAgent = request.UserAgent,
                DeviceInfo = ExtractDeviceInfo(request.UserAgent),
                IsActive = true,
                ExpiresAt = expiresAt
            };

            // Store session in cache
            var sessionKey = $"user_session:{user.UserId}";
            await _cacheService.SetAsync(sessionKey, session, TimeSpan.FromDays(request.RememberMe ? 7 : 1));

            // Store refresh token mapping
            var refreshTokenKey = $"refresh_token:{refreshToken}";
            await _cacheService.SetAsync(refreshTokenKey, new { UserId = user.UserId }, TimeSpan.FromDays(30));

            // Record login history
            await RecordLoginHistoryAsync(user.UserId, request.IpAddress, request.UserAgent, true);

            _logger.LogInformation("Successful login for user: {Username}", request.Username);

            return new LoginResponseDto
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                ExpiresAt = expiresAt,
                User = _mapper.Map<UserInfoDto>(user)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during login for username: {Username}", request.Username);
            throw;
        }
    }

    private async Task RecordLoginFailureAsync(string username, string? ipAddress)
    {
        var lockoutKey = $"login_failures:{username.ToLower()}";
        var failureInfo = await _cacheService.GetAsync<LoginFailureInfo>(lockoutKey) ?? new LoginFailureInfo { Username = username };
        
        failureInfo.RecordFailure(ipAddress);
        await _cacheService.SetAsync(lockoutKey, failureInfo, TimeSpan.FromHours(24));

        // Record failed login in history
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == username);
        if (user != null)
        {
            await RecordLoginHistoryAsync(user.UserId, ipAddress, null, false, "Invalid credentials");
        }
    }

    private async Task RecordLoginHistoryAsync(int userId, string? ipAddress, string? userAgent, bool isSuccessful, string? failureReason = null)
    {
        var historyEntry = new LoginHistoryEntry
        {
            UserId = userId,
            LoginTime = DateTime.UtcNow,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            DeviceInfo = ExtractDeviceInfo(userAgent),
            IsSuccessful = isSuccessful,
            FailureReason = failureReason
        };

        var historyKey = $"login_history:{userId}";
        var existingHistory = await _cacheService.GetAsync<List<LoginHistoryEntry>>(historyKey) ?? new List<LoginHistoryEntry>();
        
        existingHistory.Insert(0, historyEntry); // Add to beginning
        
        // Keep only last 100 entries
        if (existingHistory.Count > 100)
        {
            existingHistory = existingHistory.Take(100).ToList();
        }

        await _cacheService.SetAsync(historyKey, existingHistory, TimeSpan.FromDays(90));
    }

    private static string? ExtractDeviceInfo(string? userAgent)
    {
        if (string.IsNullOrEmpty(userAgent))
            return null;

        // Simple device detection logic
        if (userAgent.Contains("Mobile"))
            return "Mobile Device";
        if (userAgent.Contains("Tablet"))
            return "Tablet";
        if (userAgent.Contains("Chrome"))
            return "Chrome Browser";
        if (userAgent.Contains("Firefox"))
            return "Firefox Browser";
        if (userAgent.Contains("Safari"))
            return "Safari Browser";
        
        return "Desktop Browser";
    }
}

/// <summary>
/// Handler for refresh token command.
/// </summary>
public class RefreshTokenCommandHandler : IRequestHandler<RefreshTokenCommand, RefreshTokenResponseDto>
{
    private readonly ICacheService _cacheService;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IApplicationDbContext _context;
    private readonly ILogger<RefreshTokenCommandHandler> _logger;

    public RefreshTokenCommandHandler(
        ICacheService cacheService,
        IJwtTokenService jwtTokenService,
        IApplicationDbContext context,
        ILogger<RefreshTokenCommandHandler> logger)
    {
        _cacheService = cacheService;
        _jwtTokenService = jwtTokenService;
        _context = context;
        _logger = logger;
    }

    public async Task<RefreshTokenResponseDto> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        try
        {
            // Validate refresh token
            var refreshTokenKey = $"refresh_token:{request.RefreshToken}";
            var tokenData = await _cacheService.GetAsync<dynamic>(refreshTokenKey);

            if (tokenData == null)
            {
                _logger.LogWarning("Invalid refresh token used");
                throw new ValidationException("Invalid refresh token.");
            }

            var userId = (int)tokenData.GetType().GetProperty("UserId")?.GetValue(tokenData)!;

            // Get user information
            var user = await _context.Users
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken);

            if (user == null)
            {
                _logger.LogWarning("User not found for refresh token: {UserId}", userId);
                throw new ValidationException("User not found.");
            }

            // Generate new tokens
            var newAccessToken = _jwtTokenService.GenerateAccessToken(user);
            var newRefreshToken = _jwtTokenService.GenerateRefreshToken();
            var expiresAt = DateTime.UtcNow.AddHours(24);

            // Update session with new tokens
            var sessionKey = $"user_session:{userId}";
            var session = await _cacheService.GetAsync<UserSession>(sessionKey);

            if (session != null)
            {
                session.AccessToken = newAccessToken;
                session.RefreshToken = newRefreshToken;
                session.LastActiveTime = DateTime.UtcNow;
                session.ExpiresAt = expiresAt;

                await _cacheService.SetAsync(sessionKey, session, TimeSpan.FromDays(1));
            }

            // Remove old refresh token and store new one
            await _cacheService.RemoveAsync(refreshTokenKey);
            var newRefreshTokenKey = $"refresh_token:{newRefreshToken}";
            await _cacheService.SetAsync(newRefreshTokenKey, new { UserId = userId }, TimeSpan.FromDays(30));

            _logger.LogInformation("Token refreshed for user: {UserId}", userId);

            return new RefreshTokenResponseDto
            {
                AccessToken = newAccessToken,
                RefreshToken = newRefreshToken,
                ExpiresAt = expiresAt
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing token");
            throw;
        }
    }
}

/// <summary>
/// Handler for logout command.
/// </summary>
public class LogoutCommandHandler : IRequestHandler<LogoutCommand, Unit>
{
    private readonly ICacheService _cacheService;
    private readonly ILogger<LogoutCommandHandler> _logger;

    public LogoutCommandHandler(ICacheService cacheService, ILogger<LogoutCommandHandler> logger)
    {
        _cacheService = cacheService;
        _logger = logger;
    }

    public async Task<Unit> Handle(LogoutCommand request, CancellationToken cancellationToken)
    {
        try
        {
            // Get current session
            var sessionKey = $"user_session:{request.UserId}";
            var session = await _cacheService.GetAsync<UserSession>(sessionKey);

            if (session != null)
            {
                // Add access token to blacklist
                if (!string.IsNullOrEmpty(request.AccessToken))
                {
                    var blacklistKey = $"blacklisted_token:{request.AccessToken}";
                    await _cacheService.SetAsync(blacklistKey, new { UserId = request.UserId, BlacklistedAt = DateTime.UtcNow },
                        TimeSpan.FromHours(24)); // Keep blacklisted for token lifetime
                }

                // Remove refresh token
                if (!string.IsNullOrEmpty(session.RefreshToken))
                {
                    var refreshTokenKey = $"refresh_token:{session.RefreshToken}";
                    await _cacheService.RemoveAsync(refreshTokenKey);
                }

                // Update login history with logout time
                await UpdateLoginHistoryWithLogoutAsync(request.UserId, session.LoginTime);

                // Remove session
                await _cacheService.RemoveAsync(sessionKey);
            }

            _logger.LogInformation("User logged out: {UserId}", request.UserId);
            return Unit.Value;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during logout for user: {UserId}", request.UserId);
            throw;
        }
    }

    private async Task UpdateLoginHistoryWithLogoutAsync(int userId, DateTime loginTime)
    {
        var historyKey = $"login_history:{userId}";
        var history = await _cacheService.GetAsync<List<LoginHistoryEntry>>(historyKey);

        if (history != null)
        {
            var entry = history.FirstOrDefault(h => h.LoginTime == loginTime && !h.LogoutTime.HasValue);
            if (entry != null)
            {
                entry.LogoutTime = DateTime.UtcNow;
                await _cacheService.SetAsync(historyKey, history, TimeSpan.FromDays(90));
            }
        }
    }
}
