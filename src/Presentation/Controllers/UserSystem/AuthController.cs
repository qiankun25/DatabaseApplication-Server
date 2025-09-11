using DbApp.Application.UserSystem.Authentication;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace DbApp.Presentation.Controllers.UserSystem;

/// <summary>
/// Controller for authentication operations.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IMediator mediator, ILogger<AuthController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    /// <summary>
    /// Authenticates a user and returns access and refresh tokens.
    /// </summary>
    /// <param name="request">Login request containing username and password</param>
    /// <returns>Login response with tokens and user information</returns>
    [HttpPost("login")]
    public async Task<ActionResult<LoginResponseDto>> Login([FromBody] LoginRequestDto request)
    {
        try
        {
            var command = new LoginCommand(
                request.Username,
                request.Password,
                request.RememberMe,
                GetClientIpAddress(),
                Request.Headers.UserAgent.ToString()
            );

            var response = await _mediator.Send(command);
            
            _logger.LogInformation("User logged in successfully: {Username}", request.Username);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Login failed for username: {Username}", request.Username);
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Refreshes an access token using a refresh token.
    /// </summary>
    /// <param name="request">Refresh token request</param>
    /// <returns>New access and refresh tokens</returns>
    [HttpPost("refresh")]
    public async Task<ActionResult<RefreshTokenResponseDto>> RefreshToken([FromBody] RefreshTokenRequestDto request)
    {
        try
        {
            var command = new RefreshTokenCommand(request.RefreshToken);
            var response = await _mediator.Send(command);
            
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Token refresh failed");
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Logs out the current user and invalidates their session.
    /// </summary>
    /// <returns>Success response</returns>
    [HttpPost("logout")]
    [Authorize]
    public async Task<ActionResult> Logout()
    {
        try
        {
            var userId = GetCurrentUserId();
            var accessToken = GetCurrentAccessToken();
            
            var command = new LogoutCommand(userId, accessToken);
            await _mediator.Send(command);
            
            _logger.LogInformation("User logged out: {UserId}", userId);
            return Ok(new { message = "Logged out successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Logout failed");
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Initiates the password reset process.
    /// </summary>
    /// <param name="request">Forgot password request</param>
    /// <returns>Success response</returns>
    [HttpPost("forgot-password")]
    public async Task<ActionResult> ForgotPassword([FromBody] ForgotPasswordRequestDto request)
    {
        try
        {
            var command = new ForgotPasswordCommand(
                request.UsernameOrEmail,
                GetClientIpAddress(),
                Request.Headers.UserAgent.ToString()
            );

            await _mediator.Send(command);
            
            return Ok(new { message = "If the account exists, a password reset link has been sent to the associated email address." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Forgot password failed for: {UsernameOrEmail}", request.UsernameOrEmail);
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Resets a user's password using a reset token.
    /// </summary>
    /// <param name="request">Reset password request</param>
    /// <returns>Success response</returns>
    [HttpPost("reset-password")]
    public async Task<ActionResult> ResetPassword([FromBody] ResetPasswordRequestDto request)
    {
        try
        {
            var command = new ResetPasswordCommand(
                request.Token,
                request.NewPassword,
                request.ConfirmPassword
            );

            await _mediator.Send(command);
            
            return Ok(new { message = "Password has been reset successfully." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Password reset failed");
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Changes the current user's password.
    /// </summary>
    /// <param name="request">Change password request</param>
    /// <returns>Success response</returns>
    [HttpPost("change-password")]
    [Authorize]
    public async Task<ActionResult> ChangePassword([FromBody] ChangePasswordRequestDto request)
    {
        try
        {
            var userId = GetCurrentUserId();
            var command = new ChangePasswordCommand(
                userId,
                request.CurrentPassword,
                request.NewPassword,
                request.ConfirmPassword
            );

            await _mediator.Send(command);
            
            _logger.LogInformation("Password changed for user: {UserId}", userId);
            return Ok(new { message = "Password changed successfully." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Password change failed for user: {UserId}", GetCurrentUserId());
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Validates the current user's session.
    /// </summary>
    /// <returns>Session validation result</returns>
    [HttpGet("validate")]
    [Authorize]
    public async Task<ActionResult> ValidateSession()
    {
        try
        {
            var userId = GetCurrentUserId();
            var accessToken = GetCurrentAccessToken();
            
            var query = new ValidateSessionQuery(userId, accessToken);
            var isValid = await _mediator.Send(query);
            
            if (isValid)
            {
                return Ok(new { valid = true, message = "Session is valid" });
            }
            else
            {
                return Unauthorized(new { valid = false, message = "Session is invalid" });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Session validation failed");
            return Unauthorized(new { valid = false, message = "Session validation failed" });
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

    private string GetCurrentAccessToken()
    {
        var authHeader = Request.Headers.Authorization.ToString();
        if (authHeader.StartsWith("Bearer "))
        {
            return authHeader.Substring("Bearer ".Length);
        }
        return string.Empty;
    }

    private string GetClientIpAddress()
    {
        var ipAddress = Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (string.IsNullOrEmpty(ipAddress))
        {
            ipAddress = Request.HttpContext.Connection.RemoteIpAddress?.ToString();
        }
        return ipAddress ?? "Unknown";
    }
}
