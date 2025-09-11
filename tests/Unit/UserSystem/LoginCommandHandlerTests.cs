using AutoMapper;
using DbApp.Application.UserSystem.Authentication;
using DbApp.Domain.Entities.UserSystem;
using DbApp.Domain.Models.UserSystem.Authentication;
using DbApp.Domain.Services.UserSystem;
using Microsoft.Extensions.Logging;
using Moq;
using System.ComponentModel.DataAnnotations;
using ValidationException = DbApp.Application.Common.Exceptions.ValidationException;

namespace DbApp.Tests.Unit.UserSystem;

/// <summary>
/// 登录命令处理器单元测试
/// </summary>
[Trait("Category", "Unit")]
public class LoginCommandHandlerTests
{
    private readonly Mock<IAuthenticationService> _mockAuthService;
    private readonly Mock<IJwtTokenService> _mockJwtTokenService;
    private readonly Mock<ICacheService> _mockCacheService;
    private readonly Mock<IMapper> _mockMapper;
    private readonly Mock<ILogger<LoginCommandHandler>> _mockLogger;
    private readonly LoginCommandHandler _handler;

    public LoginCommandHandlerTests()
    {
        _mockAuthService = new Mock<IAuthenticationService>();
        _mockJwtTokenService = new Mock<IJwtTokenService>();
        _mockCacheService = new Mock<ICacheService>();
        _mockMapper = new Mock<IMapper>();
        _mockLogger = new Mock<ILogger<LoginCommandHandler>>();

        _handler = new LoginCommandHandler(
            null!, // IApplicationDbContext - not used in this test
            _mockAuthService.Object,
            _mockCacheService.Object,
            _mockJwtTokenService.Object,
            _mockMapper.Object,
            _mockLogger.Object
        );
    }

    private User CreateTestUser()
    {
        return new User
        {
            UserId = 1,
            Username = "testuser",
            DisplayName = "Test User",
            Email = "test@example.com",
            Role = new Role { RoleId = 1, RoleName = "TestRole" },
            PermissionLevel = 1
        };
    }

    [Fact]
    public async Task Handle_LoginCommand_WithValidCredentials_ShouldReturnLoginResponse()
    {
        // Arrange
        var command = new LoginCommand(
            Username: "testuser",
            Password: "testpassword",
            RememberMe: false,
            IpAddress: "192.168.1.1",
            UserAgent: "Test Browser"
        );

        var user = CreateTestUser();
        var accessToken = "test-access-token";
        var refreshToken = "test-refresh-token";
        var userInfoDto = new UserInfoDto
        {
            UserId = user.UserId,
            Username = user.Username,
            DisplayName = user.DisplayName,
            Email = user.Email,
            RoleName = user.Role.RoleName
        };

        _mockAuthService.Setup(x => x.ValidateCredentialsAsync("testuser", "testpassword"))
            .ReturnsAsync(user);
        _mockJwtTokenService.Setup(x => x.GenerateAccessToken(user))
            .Returns(accessToken);
        _mockJwtTokenService.Setup(x => x.GenerateRefreshToken())
            .Returns(refreshToken);
        _mockMapper.Setup(x => x.Map<UserInfoDto>(user))
            .Returns(userInfoDto);
        _mockCacheService.Setup(x => x.GetAsync<LoginFailureInfo>(It.IsAny<string>()))
            .ReturnsAsync((LoginFailureInfo?)null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(accessToken, result.AccessToken);
        Assert.Equal(refreshToken, result.RefreshToken);
        Assert.Equal(userInfoDto.UserId, result.User.UserId);
        Assert.Equal(userInfoDto.Username, result.User.Username);
        Assert.True(result.ExpiresAt > DateTime.UtcNow);
    }



    [Fact]
    public async Task Handle_LoginCommand_WithLockedAccount_ShouldThrowValidationException()
    {
        // Arrange
        var command = new LoginCommand(
            Username: "testuser",
            Password: "testpassword",
            RememberMe: false,
            IpAddress: "192.168.1.1",
            UserAgent: "Test Browser"
        );

        var lockedFailureInfo = new LoginFailureInfo
        {
            FailureCount = 5,
            LastFailureTime = DateTime.UtcNow,
            LockoutUntil = DateTime.UtcNow.AddMinutes(30) // 设置锁定时间使IsLocked为true
        };

        _mockCacheService.Setup(x => x.GetAsync<LoginFailureInfo>(It.IsAny<string>()))
            .ReturnsAsync(lockedFailureInfo);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ValidationException>(
            () => _handler.Handle(command, CancellationToken.None)
        );
        
        Assert.Equal("Account is temporarily locked due to multiple failed login attempts.", exception.Message);
    }

    [Theory]
    [InlineData(true, 24 * 7)] // RememberMe = true, 7天
    [InlineData(false, 24)] // RememberMe = false, 1天
    public async Task Handle_LoginCommand_WithRememberMe_ShouldSetCorrectExpirationTime(bool rememberMe, int expectedHours)
    {
        // Arrange
        var command = new LoginCommand(
            Username: "testuser",
            Password: "testpassword",
            RememberMe: rememberMe,
            IpAddress: "192.168.1.1",
            UserAgent: "Test Browser"
        );

        var user = CreateTestUser();
        var userInfoDto = new UserInfoDto { UserId = user.UserId, Username = user.Username };

        _mockAuthService.Setup(x => x.ValidateCredentialsAsync("testuser", "testpassword"))
            .ReturnsAsync(user);
        _mockJwtTokenService.Setup(x => x.GenerateAccessToken(user))
            .Returns("test-access-token");
        _mockJwtTokenService.Setup(x => x.GenerateRefreshToken())
            .Returns("test-refresh-token");
        _mockMapper.Setup(x => x.Map<UserInfoDto>(user))
            .Returns(userInfoDto);
        _mockCacheService.Setup(x => x.GetAsync<LoginFailureInfo>(It.IsAny<string>()))
            .ReturnsAsync((LoginFailureInfo?)null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        var expectedExpirationTime = DateTime.UtcNow.AddHours(expectedHours);
        Assert.True(Math.Abs((result.ExpiresAt - expectedExpirationTime).TotalMinutes) < 1); // 允许1分钟误差
    }

    [Fact]
    public async Task Handle_LoginCommand_ShouldStoreSessionInCache()
    {
        // Arrange
        var command = new LoginCommand(
            Username: "testuser",
            Password: "testpassword",
            RememberMe: false,
            IpAddress: "192.168.1.1",
            UserAgent: "Mozilla/5.0 Test Browser"
        );

        var user = CreateTestUser();
        var userInfoDto = new UserInfoDto { UserId = user.UserId, Username = user.Username };

        _mockAuthService.Setup(x => x.ValidateCredentialsAsync("testuser", "testpassword"))
            .ReturnsAsync(user);
        _mockJwtTokenService.Setup(x => x.GenerateAccessToken(user))
            .Returns("test-access-token");
        _mockJwtTokenService.Setup(x => x.GenerateRefreshToken())
            .Returns("test-refresh-token");
        _mockMapper.Setup(x => x.Map<UserInfoDto>(user))
            .Returns(userInfoDto);
        _mockCacheService.Setup(x => x.GetAsync<LoginFailureInfo>(It.IsAny<string>()))
            .ReturnsAsync((LoginFailureInfo?)null);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _mockCacheService.Verify(x => x.SetAsync(
            $"user_session:{user.UserId}",
            It.Is<UserSession>(s => 
                s.UserId == user.UserId &&
                s.Username == user.Username &&
                s.IpAddress == "192.168.1.1" &&
                s.UserAgent == "Mozilla/5.0 Test Browser" &&
                s.IsActive == true
            ),
            It.IsAny<TimeSpan>()
        ), Times.Once);
    }

    [Fact]
    public async Task Handle_LoginCommand_ShouldStoreRefreshTokenMapping()
    {
        // Arrange
        var command = new LoginCommand(
            Username: "testuser",
            Password: "testpassword",
            RememberMe: false,
            IpAddress: "192.168.1.1",
            UserAgent: "Test Browser"
        );

        var user = CreateTestUser();
        var refreshToken = "test-refresh-token";
        var userInfoDto = new UserInfoDto { UserId = user.UserId, Username = user.Username };

        _mockAuthService.Setup(x => x.ValidateCredentialsAsync("testuser", "testpassword"))
            .ReturnsAsync(user);
        _mockJwtTokenService.Setup(x => x.GenerateAccessToken(user))
            .Returns("test-access-token");
        _mockJwtTokenService.Setup(x => x.GenerateRefreshToken())
            .Returns(refreshToken);
        _mockMapper.Setup(x => x.Map<UserInfoDto>(user))
            .Returns(userInfoDto);
        _mockCacheService.Setup(x => x.GetAsync<LoginFailureInfo>(It.IsAny<string>()))
            .ReturnsAsync((LoginFailureInfo?)null);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _mockCacheService.Verify(x => x.SetAsync(
            $"refresh_token:{refreshToken}",
            It.Is<object>(o => o.GetType().GetProperty("UserId")!.GetValue(o)!.Equals(user.UserId)),
            TimeSpan.FromDays(30)
        ), Times.Once);
    }

    [Fact]
    public async Task Handle_LoginCommand_ShouldClearFailureRecordsOnSuccess()
    {
        // Arrange
        var command = new LoginCommand(
            Username: "testuser",
            Password: "testpassword",
            RememberMe: false,
            IpAddress: "192.168.1.1",
            UserAgent: "Test Browser"
        );

        var user = CreateTestUser();
        var userInfoDto = new UserInfoDto { UserId = user.UserId, Username = user.Username };

        _mockAuthService.Setup(x => x.ValidateCredentialsAsync("testuser", "testpassword"))
            .ReturnsAsync(user);
        _mockJwtTokenService.Setup(x => x.GenerateAccessToken(user))
            .Returns("test-access-token");
        _mockJwtTokenService.Setup(x => x.GenerateRefreshToken())
            .Returns("test-refresh-token");
        _mockMapper.Setup(x => x.Map<UserInfoDto>(user))
            .Returns(userInfoDto);
        _mockCacheService.Setup(x => x.GetAsync<LoginFailureInfo>(It.IsAny<string>()))
            .ReturnsAsync((LoginFailureInfo?)null);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _mockCacheService.Verify(x => x.RemoveAsync($"login_failures:{command.Username.ToLower()}"), Times.Once);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Handle_LoginCommand_WithEmptyUsername_ShouldThrowValidationException(string username)
    {
        // Arrange
        var command = new LoginCommand(
            Username: username,
            Password: "testpassword",
            RememberMe: false,
            IpAddress: "192.168.1.1",
            UserAgent: "Test Browser"
        );

        _mockCacheService.Setup(x => x.GetAsync<LoginFailureInfo>(It.IsAny<string>()))
            .ReturnsAsync((LoginFailureInfo?)null);
        _mockAuthService.Setup(x => x.ValidateCredentialsAsync(username, "testpassword"))
            .ReturnsAsync((User?)null);

        // Act & Assert - 空用户名应该直接返回验证失败
        var exception = await Assert.ThrowsAsync<ValidationException>(
            () => _handler.Handle(command, CancellationToken.None)
        );

        Assert.Equal("Invalid username or password.", exception.Message);
    }

    [Fact]
    public async Task Handle_LoginCommand_WithNullUsername_ShouldThrowNullReferenceException()
    {
        // Arrange
        var command = new LoginCommand(
            Username: null!,
            Password: "testpassword",
            RememberMe: false,
            IpAddress: "192.168.1.1",
            UserAgent: "Test Browser"
        );

        // Act & Assert - null用户名会导致NullReferenceException
        await Assert.ThrowsAsync<NullReferenceException>(
            () => _handler.Handle(command, CancellationToken.None)
        );
    }



    [Fact]
    public async Task Handle_LoginCommand_WithNullPassword_ShouldThrowNullReferenceException()
    {
        // Arrange
        var command = new LoginCommand(
            Username: "testuser",
            Password: null!,
            RememberMe: false,
            IpAddress: "192.168.1.1",
            UserAgent: "Test Browser"
        );

        // Act & Assert - null密码会导致NullReferenceException
        await Assert.ThrowsAsync<NullReferenceException>(
            () => _handler.Handle(command, CancellationToken.None)
        );
    }
}
