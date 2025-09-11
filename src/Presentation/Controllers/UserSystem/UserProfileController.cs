using DbApp.Application.UserSystem.Authentication;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace DbApp.Presentation.Controllers.UserSystem;

/// <summary>
/// Controller for user profile operations.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UserProfileController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<UserProfileController> _logger;

    public UserProfileController(IMediator mediator, ILogger<UserProfileController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    /// <summary>
    /// Gets the current user's profile information.
    /// </summary>
    /// <returns>User profile information</returns>
    [HttpGet("profile")]
    public async Task<ActionResult<UserInfoDto>> GetProfile()
    {
        try
        {
            var userId = GetCurrentUserId();
            var query = new GetUserProfileQuery(userId);
            var profile = await _mediator.Send(query);
            
            return Ok(profile);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user profile");
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Updates the current user's profile information.
    /// </summary>
    /// <param name="request">Profile update request</param>
    /// <returns>Updated user profile</returns>
    [HttpPut("profile")]
    public async Task<ActionResult<UserInfoDto>> UpdateProfile([FromBody] UpdateProfileRequestDto request)
    {
        try
        {
            var userId = GetCurrentUserId();
            var command = new UpdateProfileCommand(
                userId,
                request.DisplayName,
                request.Email,
                request.PhoneNumber,
                request.BirthDate,
                request.Gender
            );

            var updatedProfile = await _mediator.Send(command);
            
            _logger.LogInformation("Profile updated for user: {UserId}", userId);
            return Ok(updatedProfile);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating user profile");
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Gets the current user's login history.
    /// </summary>
    /// <param name="pageNumber">Page number for pagination</param>
    /// <param name="pageSize">Page size for pagination</param>
    /// <returns>List of login history entries</returns>
    [HttpGet("login-history")]
    public async Task<ActionResult<List<LoginHistoryDto>>> GetLoginHistory(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20)
    {
        try
        {
            var userId = GetCurrentUserId();
            var query = new GetLoginHistoryQuery(userId, pageNumber, pageSize);
            var history = await _mediator.Send(query);
            
            return Ok(history);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting login history");
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Gets the current user's active sessions.
    /// </summary>
    /// <returns>List of active sessions</returns>
    [HttpGet("sessions")]
    public async Task<ActionResult<List<UserSessionDto>>> GetSessions()
    {
        try
        {
            var userId = GetCurrentUserId();
            var query = new GetUserSessionsQuery(userId);
            var sessions = await _mediator.Send(query);
            
            return Ok(sessions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user sessions");
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Gets basic user information for the current user.
    /// </summary>
    /// <returns>Basic user information</returns>
    [HttpGet("me")]
    public ActionResult<object> GetCurrentUser()
    {
        try
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            var usernameClaim = User.FindFirst(ClaimTypes.Name);
            var emailClaim = User.FindFirst(ClaimTypes.Email);
            var roleClaim = User.FindFirst(ClaimTypes.Role);
            var displayNameClaim = User.FindFirst("display_name");
            var permissionLevelClaim = User.FindFirst("permission_level");

            var userInfo = new
            {
                UserId = userIdClaim?.Value,
                Username = usernameClaim?.Value,
                Email = emailClaim?.Value,
                Role = roleClaim?.Value,
                DisplayName = displayNameClaim?.Value,
                PermissionLevel = permissionLevelClaim?.Value
            };

            return Ok(userInfo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting current user info");
            return BadRequest(new { message = ex.Message });
        }
    }

    private int GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
        {
            return userId;
        }
        throw new UnauthorizedAccessException("User ID not found in token");
    }
}
