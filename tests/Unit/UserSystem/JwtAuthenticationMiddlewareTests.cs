using DbApp.Domain.Models.UserSystem.Authentication;
using DbApp.Domain.Services.UserSystem;
using DbApp.Infrastructure.Middleware.UserSystem;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using System.Security.Claims;

namespace DbApp.Tests.Unit.UserSystem;

/// <summary>
/// JWT认证中间件单元测试
/// </summary>
[Trait("Category", "Unit")]
public class JwtAuthenticationMiddlewareTests
{
    private readonly Mock<RequestDelegate> _mockNext;
    private readonly Mock<ILogger<JwtAuthenticationMiddleware>> _mockLogger;
    private readonly Mock<IJwtTokenService> _mockJwtTokenService;
    private readonly Mock<ICacheService> _mockCacheService;
    private readonly JwtAuthenticationMiddleware _middleware;

    public JwtAuthenticationMiddlewareTests()
    {
        _mockNext = new Mock<RequestDelegate>();
        _mockLogger = new Mock<ILogger<JwtAuthenticationMiddleware>>();
        _mockJwtTokenService = new Mock<IJwtTokenService>();
        _mockCacheService = new Mock<ICacheService>();

        _middleware = new JwtAuthenticationMiddleware(_mockNext.Object, _mockLogger.Object);
    }

    private DefaultHttpContext CreateHttpContext(string? authorizationHeader = null)
    {
        var context = new DefaultHttpContext();
        if (!string.IsNullOrEmpty(authorizationHeader))
        {
            context.Request.Headers.Authorization = authorizationHeader;
        }
        return context;
    }

    private ClaimsPrincipal CreateClaimsPrincipal(int userId = 1, string username = "testuser")
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Name, username),
            new Claim(ClaimTypes.Email, "test@example.com"),
            new Claim(ClaimTypes.Role, "TestRole")
        };

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "jwt"));
    }

    [Fact]
    public async Task InvokeAsync_WithoutAuthorizationHeader_ShouldCallNext()
    {
        // Arrange
        var context = CreateHttpContext();

        // Act
        await _middleware.InvokeAsync(context, _mockJwtTokenService.Object, _mockCacheService.Object);

        // Assert
        _mockNext.Verify(x => x(context), Times.Once);
        Assert.False(context.User.Identity?.IsAuthenticated ?? false);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("InvalidHeader")]
    [InlineData("Basic dGVzdDp0ZXN0")] // Basic auth instead of Bearer
    public async Task InvokeAsync_WithInvalidAuthorizationHeader_ShouldCallNext(string authHeader)
    {
        // Arrange
        var context = CreateHttpContext(authHeader);

        // Act
        await _middleware.InvokeAsync(context, _mockJwtTokenService.Object, _mockCacheService.Object);

        // Assert
        _mockNext.Verify(x => x(context), Times.Once);
        Assert.False(context.User.Identity?.IsAuthenticated ?? false);
    }

    [Fact]
    public async Task InvokeAsync_WithValidToken_ShouldSetUserAndCallNext()
    {
        // Arrange
        var token = "valid-jwt-token";
        var context = CreateHttpContext($"Bearer {token}");
        var claimsPrincipal = CreateClaimsPrincipal();

        _mockJwtTokenService.Setup(x => x.ValidateToken(token))
            .Returns(claimsPrincipal);
        _mockCacheService.Setup(x => x.ExistsAsync($"blacklisted_token:{token}"))
            .ReturnsAsync(false);

        // Act
        await _middleware.InvokeAsync(context, _mockJwtTokenService.Object, _mockCacheService.Object);

        // Assert
        _mockNext.Verify(x => x(context), Times.Once);
        Assert.Equal(claimsPrincipal, context.User);
        Assert.True(context.User.Identity!.IsAuthenticated);
    }

    [Fact]
    public async Task InvokeAsync_WithBlacklistedToken_ShouldNotSetUserAndNotCallNext()
    {
        // Arrange
        var token = "blacklisted-jwt-token";
        var context = CreateHttpContext($"Bearer {token}");
        var claimsPrincipal = CreateClaimsPrincipal();

        _mockJwtTokenService.Setup(x => x.ValidateToken(token))
            .Returns(claimsPrincipal);
        _mockCacheService.Setup(x => x.ExistsAsync($"blacklisted_token:{token}"))
            .ReturnsAsync(true);

        // Act
        await _middleware.InvokeAsync(context, _mockJwtTokenService.Object, _mockCacheService.Object);

        // Assert - 黑名单令牌应该直接返回401，不调用next
        _mockNext.Verify(x => x(context), Times.Never);
        Assert.False(context.User.Identity?.IsAuthenticated ?? false);
        Assert.Equal(401, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_WithInvalidToken_ShouldNotSetUserAndCallNext()
    {
        // Arrange
        var token = "invalid-jwt-token";
        var context = CreateHttpContext($"Bearer {token}");

        _mockJwtTokenService.Setup(x => x.ValidateToken(token))
            .Returns((ClaimsPrincipal?)null);

        // Act
        await _middleware.InvokeAsync(context, _mockJwtTokenService.Object, _mockCacheService.Object);

        // Assert
        _mockNext.Verify(x => x(context), Times.Once);
        Assert.False(context.User.Identity?.IsAuthenticated ?? false);
    }

    [Fact]
    public async Task InvokeAsync_WithValidToken_ShouldUpdateSessionActivity()
    {
        // Arrange
        var token = "valid-jwt-token";
        var userId = 123;
        var context = CreateHttpContext($"Bearer {token}");
        var claimsPrincipal = CreateClaimsPrincipal(userId, "testuser");

        var userSession = new UserSession
        {
            UserId = userId,
            Username = "testuser",
            AccessToken = token,
            IsActive = true,
            LastActiveTime = DateTime.UtcNow.AddMinutes(-10)
        };

        _mockJwtTokenService.Setup(x => x.ValidateToken(token))
            .Returns(claimsPrincipal);
        _mockCacheService.Setup(x => x.ExistsAsync($"blacklisted_token:{token}"))
            .ReturnsAsync(false);
        _mockCacheService.Setup(x => x.GetAsync<UserSession>($"user_session:{userId}"))
            .ReturnsAsync(userSession);

        // Act
        await _middleware.InvokeAsync(context, _mockJwtTokenService.Object, _mockCacheService.Object);

        // Assert
        _mockCacheService.Verify(x => x.SetAsync(
            $"user_session:{userId}",
            It.IsAny<UserSession>(),
            It.IsAny<TimeSpan>()
        ), Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_WithValidTokenButNoSession_ShouldNotUpdateSession()
    {
        // Arrange
        var token = "valid-jwt-token";
        var userId = 123;
        var context = CreateHttpContext($"Bearer {token}");
        var claimsPrincipal = CreateClaimsPrincipal(userId, "testuser");

        _mockJwtTokenService.Setup(x => x.ValidateToken(token))
            .Returns(claimsPrincipal);
        _mockCacheService.Setup(x => x.ExistsAsync($"blacklisted_token:{token}"))
            .ReturnsAsync(false);
        _mockCacheService.Setup(x => x.GetAsync<UserSession>($"user_session:{userId}"))
            .ReturnsAsync((UserSession?)null);

        // Act
        await _middleware.InvokeAsync(context, _mockJwtTokenService.Object, _mockCacheService.Object);

        // Assert
        _mockCacheService.Verify(x => x.SetAsync(
            It.IsAny<string>(),
            It.IsAny<UserSession>(),
            It.IsAny<TimeSpan>()
        ), Times.Never);
    }

    [Fact]
    public async Task InvokeAsync_WithValidTokenButInactiveSession_ShouldNotUpdateSession()
    {
        // Arrange
        var token = "valid-jwt-token";
        var userId = 123;
        var context = CreateHttpContext($"Bearer {token}");
        var claimsPrincipal = CreateClaimsPrincipal(userId, "testuser");

        var inactiveSession = new UserSession
        {
            UserId = userId,
            Username = "testuser",
            AccessToken = token,
            IsActive = false, // 会话已失效
            LastActiveTime = DateTime.UtcNow.AddMinutes(-10)
        };

        _mockJwtTokenService.Setup(x => x.ValidateToken(token))
            .Returns(claimsPrincipal);
        _mockCacheService.Setup(x => x.ExistsAsync($"blacklisted_token:{token}"))
            .ReturnsAsync(false);
        _mockCacheService.Setup(x => x.GetAsync<UserSession>($"user_session:{userId}"))
            .ReturnsAsync(inactiveSession);

        // Act
        await _middleware.InvokeAsync(context, _mockJwtTokenService.Object, _mockCacheService.Object);

        // Assert
        _mockCacheService.Verify(x => x.SetAsync(
            It.IsAny<string>(),
            It.IsAny<UserSession>(),
            It.IsAny<TimeSpan>()
        ), Times.Never);
    }

    [Fact]
    public async Task InvokeAsync_WithValidTokenButDifferentTokenInSession_ShouldNotUpdateSession()
    {
        // Arrange
        var token = "valid-jwt-token";
        var userId = 123;
        var context = CreateHttpContext($"Bearer {token}");
        var claimsPrincipal = CreateClaimsPrincipal(userId, "testuser");

        var sessionWithDifferentToken = new UserSession
        {
            UserId = userId,
            Username = "testuser",
            AccessToken = "different-token", // 不同的令牌
            IsActive = true,
            LastActiveTime = DateTime.UtcNow.AddMinutes(-10)
        };

        _mockJwtTokenService.Setup(x => x.ValidateToken(token))
            .Returns(claimsPrincipal);
        _mockCacheService.Setup(x => x.ExistsAsync($"blacklisted_token:{token}"))
            .ReturnsAsync(false);
        _mockCacheService.Setup(x => x.GetAsync<UserSession>($"user_session:{userId}"))
            .ReturnsAsync(sessionWithDifferentToken);

        // Act
        await _middleware.InvokeAsync(context, _mockJwtTokenService.Object, _mockCacheService.Object);

        // Assert
        _mockCacheService.Verify(x => x.SetAsync(
            It.IsAny<string>(),
            It.IsAny<UserSession>(),
            It.IsAny<TimeSpan>()
        ), Times.Never);
    }

    [Fact]
    public async Task InvokeAsync_WhenExceptionOccurs_ShouldLogAndCallNext()
    {
        // Arrange
        var token = "valid-jwt-token";
        var context = CreateHttpContext($"Bearer {token}");

        _mockJwtTokenService.Setup(x => x.ValidateToken(token))
            .Throws(new Exception("Token validation error"));

        // Act
        await _middleware.InvokeAsync(context, _mockJwtTokenService.Object, _mockCacheService.Object);

        // Assert
        _mockNext.Verify(x => x(context), Times.Once);
        Assert.False(context.User.Identity?.IsAuthenticated ?? false);
    }

    [Theory]
    [InlineData("Bearer")]
    [InlineData("Bearer ")]
    [InlineData("Bearer  ")]
    public async Task InvokeAsync_WithBearerButNoToken_ShouldCallNext(string authHeader)
    {
        // Arrange
        var context = CreateHttpContext(authHeader);

        // Act
        await _middleware.InvokeAsync(context, _mockJwtTokenService.Object, _mockCacheService.Object);

        // Assert
        _mockNext.Verify(x => x(context), Times.Once);
        Assert.False(context.User.Identity?.IsAuthenticated ?? false);
    }


}
