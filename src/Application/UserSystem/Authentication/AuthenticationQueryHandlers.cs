using AutoMapper;
using DbApp.Application.Common.Exceptions;
using DbApp.Application.Common.Interfaces;
using DbApp.Domain.Models.UserSystem.Authentication;
using DbApp.Domain.Services.UserSystem;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DbApp.Application.UserSystem.Authentication;

/// <summary>
/// Handler for get user profile query.
/// </summary>
public class GetUserProfileQueryHandler : IRequestHandler<GetUserProfileQuery, UserInfoDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IMapper _mapper;
    private readonly ILogger<GetUserProfileQueryHandler> _logger;

    public GetUserProfileQueryHandler(
        IApplicationDbContext context,
        IMapper mapper,
        ILogger<GetUserProfileQueryHandler> logger)
    {
        _context = context;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<UserInfoDto> Handle(GetUserProfileQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var user = await _context.Users
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.UserId == request.UserId, cancellationToken);

            if (user == null)
            {
                throw new ValidationException("User not found.");
            }

            return _mapper.Map<UserInfoDto>(user);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user profile: {UserId}", request.UserId);
            throw;
        }
    }
}

/// <summary>
/// Handler for get login history query.
/// </summary>
public class GetLoginHistoryQueryHandler : IRequestHandler<GetLoginHistoryQuery, List<LoginHistoryDto>>
{
    private readonly ICacheService _cacheService;
    private readonly IMapper _mapper;
    private readonly ILogger<GetLoginHistoryQueryHandler> _logger;

    public GetLoginHistoryQueryHandler(
        ICacheService cacheService,
        IMapper mapper,
        ILogger<GetLoginHistoryQueryHandler> logger)
    {
        _cacheService = cacheService;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<List<LoginHistoryDto>> Handle(GetLoginHistoryQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var historyKey = $"login_history:{request.UserId}";
            var history = await _cacheService.GetAsync<List<LoginHistoryEntry>>(historyKey) ?? new List<LoginHistoryEntry>();

            // Apply pagination
            var pagedHistory = history
                .Skip((request.PageNumber - 1) * request.PageSize)
                .Take(request.PageSize)
                .ToList();

            return _mapper.Map<List<LoginHistoryDto>>(pagedHistory);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting login history for user: {UserId}", request.UserId);
            throw;
        }
    }
}

/// <summary>
/// Handler for get user sessions query.
/// </summary>
public class GetUserSessionsQueryHandler : IRequestHandler<GetUserSessionsQuery, List<UserSessionDto>>
{
    private readonly ICacheService _cacheService;
    private readonly IMapper _mapper;
    private readonly ILogger<GetUserSessionsQueryHandler> _logger;

    public GetUserSessionsQueryHandler(
        ICacheService cacheService,
        IMapper mapper,
        ILogger<GetUserSessionsQueryHandler> logger)
    {
        _cacheService = cacheService;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<List<UserSessionDto>> Handle(GetUserSessionsQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var sessionKey = $"user_session:{request.UserId}";
            var session = await _cacheService.GetAsync<UserSession>(sessionKey);

            var sessions = new List<UserSession>();
            if (session != null && session.IsActive)
            {
                sessions.Add(session);
            }

            return _mapper.Map<List<UserSessionDto>>(sessions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user sessions: {UserId}", request.UserId);
            throw;
        }
    }
}

/// <summary>
/// Handler for validate session query.
/// </summary>
public class ValidateSessionQueryHandler : IRequestHandler<ValidateSessionQuery, bool>
{
    private readonly ICacheService _cacheService;
    private readonly ILogger<ValidateSessionQueryHandler> _logger;

    public ValidateSessionQueryHandler(
        ICacheService cacheService,
        ILogger<ValidateSessionQueryHandler> logger)
    {
        _cacheService = cacheService;
        _logger = logger;
    }

    public async Task<bool> Handle(ValidateSessionQuery request, CancellationToken cancellationToken)
    {
        try
        {
            // Check if token is blacklisted
            var blacklistKey = $"blacklisted_token:{request.AccessToken}";
            var isBlacklisted = await _cacheService.ExistsAsync(blacklistKey);
            
            if (isBlacklisted)
            {
                return false;
            }

            // Check if session exists and is active
            var sessionKey = $"user_session:{request.UserId}";
            var session = await _cacheService.GetAsync<UserSession>(sessionKey);

            if (session == null || !session.IsActive || session.AccessToken != request.AccessToken)
            {
                return false;
            }

            // Check if session has expired
            if (session.ExpiresAt <= DateTime.UtcNow)
            {
                // Remove expired session
                await _cacheService.RemoveAsync(sessionKey);
                return false;
            }

            // Update last active time
            session.LastActiveTime = DateTime.UtcNow;
            await _cacheService.SetAsync(sessionKey, session, TimeSpan.FromDays(1));

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating session for user: {UserId}", request.UserId);
            return false;
        }
    }
}
