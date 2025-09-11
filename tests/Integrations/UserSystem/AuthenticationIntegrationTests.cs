using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DbApp.Application.UserSystem.Authentication;
using DbApp.Domain.Entities.UserSystem;
using DbApp.Infrastructure;
using DbApp.Infrastructure.Services.UserSystem;
using DbApp.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

namespace DbApp.Tests.Integrations.UserSystem;

/// <summary>
/// 用户认证集成测试
/// </summary>
[Collection("Database")]
public class AuthenticationIntegrationTests(DatabaseFixture fixture) : IAsyncLifetime
{
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private User? _testUser;
    private Role? _testRole;
    private TestApiFactory? _factory;
    private HttpClient? _client;

    public async Task InitializeAsync()
    {
        _factory = new TestApiFactory(fixture);
        _client = _factory.CreateClient();
        await AddTestUserData();
    }

    public async Task DisposeAsync()
    {
        await RemoveTestUserData();
        _client?.Dispose();
        _factory?.Dispose();
    }

    private async Task AddTestUserData()
    {
        using var scope = _factory!.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // 创建测试角色
        _testRole = new Role
        {
            RoleName = "TestRole",
            IsSystemRole = true,
            CreatedAt = DateTime.UtcNow
        };
        db.Roles.Add(_testRole);
        await db.SaveChangesAsync();

        // 创建认证服务来哈希密码
        var logger = new Mock<ILogger<AuthenticationService>>().Object;
        var authService = new AuthenticationService(db, logger);
        var hashedPassword = authService.HashPassword("testpassword123");

        // 创建测试用户
        _testUser = new User
        {
            Username = "testuser",
            PasswordHash = hashedPassword,
            Email = "test@example.com",
            DisplayName = "Test User",
            PhoneNumber = "1234567890",
            RoleId = _testRole.RoleId,
            PermissionLevel = 1,
            CreatedAt = DateTime.UtcNow,
            RegisterTime = DateTime.UtcNow
        };

        db.Users.Add(_testUser);
        await db.SaveChangesAsync();

        Console.WriteLine($"✓ Added test user: {_testUser.Username} (ID: {_testUser.UserId})");
    }

    private async Task RemoveTestUserData()
    {
        try
        {
            if (_factory == null) return;

            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            if (_testUser != null)
            {
                var user = await db.Users.FirstOrDefaultAsync(u => u.UserId == _testUser.UserId);
                if (user != null)
                {
                    db.Users.Remove(user);
                }
            }

            if (_testRole != null)
            {
                var role = await db.Roles.FirstOrDefaultAsync(r => r.RoleId == _testRole.RoleId);
                if (role != null)
                {
                    db.Roles.Remove(role);
                }
            }

            await db.SaveChangesAsync();
            Console.WriteLine("✓ Cleaned up test authentication data");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠ Error during cleanup: {ex.Message}");
            // Don't throw during cleanup to avoid masking test failures
        }
    }

    [Fact]
    public async Task Login_WithValidCredentials_ShouldReturnSuccessWithTokens()
    {
        // Arrange
        var loginRequest = new LoginRequestDto
        {
            Username = "testuser",
            Password = "testpassword123",
            RememberMe = false
        };

        // Act
        var response = await _client!.PostAsJsonAsync("/api/auth/login", loginRequest, _jsonOptions);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var jsonContent = await response.Content.ReadAsStringAsync();
        var loginResponse = JsonSerializer.Deserialize<LoginResponseDto>(jsonContent, _jsonOptions);

        Assert.NotNull(loginResponse);
        Assert.NotEmpty(loginResponse.AccessToken);
        Assert.NotEmpty(loginResponse.RefreshToken);
        Assert.True(loginResponse.ExpiresAt > DateTime.UtcNow);
        Assert.Equal(_testUser!.UserId, loginResponse.User.UserId);
        Assert.Equal(_testUser.Username, loginResponse.User.Username);
        Assert.Equal(_testUser.DisplayName, loginResponse.User.DisplayName);
        Assert.Equal(_testUser.Email, loginResponse.User.Email);

        Console.WriteLine($"✓ Login successful for user: {loginResponse.User.Username}");
    }

    [Fact]
    public async Task Login_WithInvalidUsername_ShouldReturnBadRequest()
    {
        // Arrange
        using var factory = new TestApiFactory(fixture);
        using var client = factory.CreateClient();

        var loginRequest = new LoginRequestDto
        {
            Username = "nonexistentuser",
            Password = "testpassword123",
            RememberMe = false
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/auth/login", loginRequest, _jsonOptions);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var jsonContent = await response.Content.ReadAsStringAsync();
        Assert.Contains("Invalid username or password", jsonContent);

        Console.WriteLine("✓ Login correctly rejected invalid username");
    }

    [Fact]
    public async Task Login_WithInvalidPassword_ShouldReturnBadRequest()
    {
        // Arrange
        using var factory = new TestApiFactory(fixture);
        using var client = factory.CreateClient();

        var loginRequest = new LoginRequestDto
        {
            Username = "testuser",
            Password = "wrongpassword",
            RememberMe = false
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/auth/login", loginRequest, _jsonOptions);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var jsonContent = await response.Content.ReadAsStringAsync();
        Assert.Contains("Invalid username or password", jsonContent);

        Console.WriteLine("✓ Login correctly rejected invalid password");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Login_WithRememberMeOption_ShouldSetCorrectExpirationTime(bool rememberMe)
    {
        // Arrange
        using var factory = new TestApiFactory(fixture);
        using var client = factory.CreateClient();

        var loginRequest = new LoginRequestDto
        {
            Username = "testuser",
            Password = "testpassword123",
            RememberMe = rememberMe
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/auth/login", loginRequest, _jsonOptions);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var jsonContent = await response.Content.ReadAsStringAsync();
        var loginResponse = JsonSerializer.Deserialize<LoginResponseDto>(jsonContent, _jsonOptions);

        Assert.NotNull(loginResponse);

        var expectedHours = rememberMe ? 24 * 7 : 24; // 7天或1天
        var expectedExpirationTime = DateTime.UtcNow.AddHours(expectedHours);
        var timeDifference = Math.Abs((loginResponse.ExpiresAt - expectedExpirationTime).TotalMinutes);
        
        Assert.True(timeDifference < 5, $"Expiration time difference too large: {timeDifference} minutes");

        Console.WriteLine($"✓ Login with RememberMe={rememberMe} set correct expiration time");
    }

    [Fact]
    public async Task Login_MultipleFailedAttempts_ShouldEventuallyLockAccount()
    {
        // Arrange
        using var factory = new TestApiFactory(fixture);
        using var client = factory.CreateClient();

        var loginRequest = new LoginRequestDto
        {
            Username = "testuser",
            Password = "wrongpassword",
            RememberMe = false
        };

        // Act - 进行多次失败的登录尝试
        for (int i = 0; i < 5; i++)
        {
            var response = await client.PostAsJsonAsync("/api/auth/login", loginRequest, _jsonOptions);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        // 第6次尝试应该被锁定
        var lockedResponse = await client.PostAsJsonAsync("/api/auth/login", loginRequest, _jsonOptions);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, lockedResponse.StatusCode);
        var jsonContent = await lockedResponse.Content.ReadAsStringAsync();
        Assert.Contains("locked", jsonContent.ToLower());

        Console.WriteLine("✓ Account correctly locked after multiple failed attempts");
    }

    [Fact]
    public async Task RefreshToken_WithValidToken_ShouldReturnNewAccessToken()
    {
        // Arrange
        using var factory = new TestApiFactory(fixture);
        using var client = factory.CreateClient();

        // 首先登录获取刷新令牌
        var loginRequest = new LoginRequestDto
        {
            Username = "testuser",
            Password = "testpassword123",
            RememberMe = false
        };

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", loginRequest, _jsonOptions);
        var loginContent = await loginResponse.Content.ReadAsStringAsync();
        var loginResult = JsonSerializer.Deserialize<LoginResponseDto>(loginContent, _jsonOptions);

        var refreshRequest = new RefreshTokenRequestDto
        {
            RefreshToken = loginResult!.RefreshToken
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/auth/refresh", refreshRequest, _jsonOptions);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var jsonContent = await response.Content.ReadAsStringAsync();
        var refreshResponse = JsonSerializer.Deserialize<RefreshTokenResponseDto>(jsonContent, _jsonOptions);

        Assert.NotNull(refreshResponse);
        Assert.NotEmpty(refreshResponse.AccessToken);
        Assert.NotEmpty(refreshResponse.RefreshToken);
        Assert.NotEqual(loginResult.AccessToken, refreshResponse.AccessToken);
        Assert.NotEqual(loginResult.RefreshToken, refreshResponse.RefreshToken);

        Console.WriteLine("✓ Token refresh successful");
    }

    [Fact]
    public async Task RefreshToken_WithInvalidToken_ShouldReturnBadRequest()
    {
        // Arrange
        using var factory = new TestApiFactory(fixture);
        using var client = factory.CreateClient();

        var refreshRequest = new RefreshTokenRequestDto
        {
            RefreshToken = "invalid-refresh-token"
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/auth/refresh", refreshRequest, _jsonOptions);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        Console.WriteLine("✓ Token refresh correctly rejected invalid token");
    }

    [Fact]
    public async Task Logout_WithValidToken_ShouldReturnSuccess()
    {
        // Arrange
        using var factory = new TestApiFactory(fixture);
        using var client = factory.CreateClient();

        // 首先登录获取访问令牌
        var loginRequest = new LoginRequestDto
        {
            Username = "testuser",
            Password = "testpassword123",
            RememberMe = false
        };

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", loginRequest, _jsonOptions);
        var loginContent = await loginResponse.Content.ReadAsStringAsync();
        var loginResult = JsonSerializer.Deserialize<LoginResponseDto>(loginContent, _jsonOptions);

        // 设置授权头
        client.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", loginResult!.AccessToken);

        // Act
        var response = await client.PostAsync("/api/auth/logout", null);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        Console.WriteLine("✓ Logout successful");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task Login_WithEmptyCredentials_ShouldReturnBadRequest(string emptyValue)
    {
        // Arrange
        using var factory = new TestApiFactory(fixture);
        using var client = factory.CreateClient();

        var loginRequest = new LoginRequestDto
        {
            Username = emptyValue ?? "",
            Password = emptyValue ?? "",
            RememberMe = false
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/auth/login", loginRequest, _jsonOptions);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        Console.WriteLine("✓ Login correctly rejected empty credentials");
    }
}
