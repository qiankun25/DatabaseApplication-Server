using MediatR;

namespace DbApp.Application.UserSystem.Authentication;

/// <summary>
/// Query to get user profile information.
/// </summary>
public record GetUserProfileQuery(
    int UserId
) : IRequest<UserInfoDto>;

/// <summary>
/// Query to get user login history.
/// </summary>
public record GetLoginHistoryQuery(
    int UserId,
    int PageNumber = 1,
    int PageSize = 20
) : IRequest<List<LoginHistoryDto>>;

/// <summary>
/// Query to get current user sessions.
/// </summary>
public record GetUserSessionsQuery(
    int UserId
) : IRequest<List<UserSessionDto>>;

/// <summary>
/// Query to validate if a user session is active.
/// </summary>
public record ValidateSessionQuery(
    int UserId,
    string AccessToken
) : IRequest<bool>;
