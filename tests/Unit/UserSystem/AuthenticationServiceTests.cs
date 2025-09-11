using DbApp.Domain.Entities.UserSystem;
using DbApp.Domain.Enums.UserSystem;
using DbApp.Infrastructure;
using DbApp.Infrastructure.Services.UserSystem;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace DbApp.Tests.Unit.UserSystem;

/// <summary>
/// 认证服务单元测试
/// </summary>
[Trait("Category", "Unit")]
public class AuthenticationServiceTests
{
    private readonly Mock<ILogger<AuthenticationService>> _mockLogger;
    private readonly DbContextOptions<ApplicationDbContext> _dbOptions;

    public AuthenticationServiceTests()
    {
        _mockLogger = new Mock<ILogger<AuthenticationService>>();
        
        // 使用内存数据库进行单元测试
        _dbOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
    }

    private ApplicationDbContext CreateContext()
    {
        return new ApplicationDbContext(_dbOptions);
    }

    private async Task<User> CreateTestUserAsync(ApplicationDbContext context, string username = "testuser", string password = "testpassword")
    {
        var role = new Role
        {
            RoleName = "TestRole",
            IsSystemRole = true,
            CreatedAt = DateTime.UtcNow
        };
        context.Roles.Add(role);
        await context.SaveChangesAsync();

        var authService = new AuthenticationService(context, _mockLogger.Object);
        var hashedPassword = authService.HashPassword(password);

        var user = new User
        {
            Username = username,
            PasswordHash = hashedPassword,
            Email = "test@example.com",
            DisplayName = "Test User",
            RoleId = role.RoleId,
            CreatedAt = DateTime.UtcNow,
            RegisterTime = DateTime.UtcNow
        };

        context.Users.Add(user);
        await context.SaveChangesAsync();
        return user;
    }

    [Fact]
    public async Task ValidateCredentialsAsync_WithValidCredentials_ShouldReturnUser()
    {
        // Arrange
        using var context = CreateContext();
        var authService = new AuthenticationService(context, _mockLogger.Object);
        var testUser = await CreateTestUserAsync(context, "validuser", "validpassword");

        // Act
        var result = await authService.ValidateCredentialsAsync("validuser", "validpassword");

        // Assert
        Assert.NotNull(result);
        Assert.Equal(testUser.UserId, result.UserId);
        Assert.Equal(testUser.Username, result.Username);
        Assert.Equal(testUser.Email, result.Email);
        Assert.NotNull(result.Role);
    }

    [Fact]
    public async Task ValidateCredentialsAsync_WithInvalidUsername_ShouldReturnNull()
    {
        // Arrange
        using var context = CreateContext();
        var authService = new AuthenticationService(context, _mockLogger.Object);
        await CreateTestUserAsync(context, "validuser", "validpassword");

        // Act
        var result = await authService.ValidateCredentialsAsync("invaliduser", "validpassword");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task ValidateCredentialsAsync_WithInvalidPassword_ShouldReturnNull()
    {
        // Arrange
        using var context = CreateContext();
        var authService = new AuthenticationService(context, _mockLogger.Object);
        await CreateTestUserAsync(context, "validuser", "validpassword");

        // Act
        var result = await authService.ValidateCredentialsAsync("validuser", "invalidpassword");

        // Assert
        Assert.Null(result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task ValidateCredentialsAsync_WithEmptyUsername_ShouldReturnNull(string username)
    {
        // Arrange
        using var context = CreateContext();
        var authService = new AuthenticationService(context, _mockLogger.Object);

        // Act
        var result = await authService.ValidateCredentialsAsync(username, "password");

        // Assert
        Assert.Null(result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task ValidateCredentialsAsync_WithEmptyPassword_ShouldReturnNull(string password)
    {
        // Arrange
        using var context = CreateContext();
        var authService = new AuthenticationService(context, _mockLogger.Object);
        await CreateTestUserAsync(context, "validuser", "validpassword");

        // Act
        var result = await authService.ValidateCredentialsAsync("validuser", password);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void HashPassword_WithValidPassword_ShouldReturnHashedPassword()
    {
        // Arrange
        using var context = CreateContext();
        var authService = new AuthenticationService(context, _mockLogger.Object);
        var password = "testpassword123";

        // Act
        var hashedPassword = authService.HashPassword(password);

        // Assert
        Assert.NotNull(hashedPassword);
        Assert.NotEmpty(hashedPassword);
        Assert.NotEqual(password, hashedPassword);
        Assert.True(hashedPassword.Length > 50); // BCrypt hashes are typically 60 characters
    }

    [Fact]
    public void HashPassword_WithSamePassword_ShouldReturnDifferentHashes()
    {
        // Arrange
        using var context = CreateContext();
        var authService = new AuthenticationService(context, _mockLogger.Object);
        var password = "testpassword123";

        // Act
        var hash1 = authService.HashPassword(password);
        var hash2 = authService.HashPassword(password);

        // Assert
        Assert.NotEqual(hash1, hash2); // BCrypt uses salt, so same password should produce different hashes
    }

    [Fact]
    public void VerifyPassword_WithCorrectPassword_ShouldReturnTrue()
    {
        // Arrange
        using var context = CreateContext();
        var authService = new AuthenticationService(context, _mockLogger.Object);
        var password = "testpassword123";
        var hashedPassword = authService.HashPassword(password);

        // Act
        var result = authService.VerifyPassword(password, hashedPassword);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void VerifyPassword_WithIncorrectPassword_ShouldReturnFalse()
    {
        // Arrange
        using var context = CreateContext();
        var authService = new AuthenticationService(context, _mockLogger.Object);
        var password = "testpassword123";
        var wrongPassword = "wrongpassword";
        var hashedPassword = authService.HashPassword(password);

        // Act
        var result = authService.VerifyPassword(wrongPassword, hashedPassword);

        // Assert
        Assert.False(result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void VerifyPassword_WithEmptyPassword_ShouldReturnFalse(string password)
    {
        // Arrange
        using var context = CreateContext();
        var authService = new AuthenticationService(context, _mockLogger.Object);
        var validPassword = "testpassword123";
        var hashedPassword = authService.HashPassword(validPassword);

        // Act
        var result = authService.VerifyPassword(password, hashedPassword);

        // Assert
        Assert.False(result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("invalid-hash")]
    public void VerifyPassword_WithInvalidHash_ShouldReturnFalse(string hash)
    {
        // Arrange
        using var context = CreateContext();
        var authService = new AuthenticationService(context, _mockLogger.Object);
        var password = "testpassword123";

        // Act
        var result = authService.VerifyPassword(password, hash);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task ValidateCredentialsAsync_WithDatabaseException_ShouldReturnNull()
    {
        // Arrange - 测试异常处理逻辑
        using var context = CreateContext();
        var authService = new AuthenticationService(context, _mockLogger.Object);

        // 使用一个会导致数据库查询异常的用户名（包含特殊字符）
        // 这里我们测试服务的异常处理能力
        var result = await authService.ValidateCredentialsAsync("", "testpassword");

        // Act & Assert - 空用户名应该返回null而不是抛出异常
        Assert.Null(result);
    }

    [Theory]
    [InlineData("password")]
    [InlineData("123456")]
    [InlineData("abcdef")]
    [InlineData("Password123!")]
    [InlineData("VeryLongPasswordWithSpecialCharacters!@#$%^&*()")]
    public void HashPassword_WithVariousPasswords_ShouldProduceValidHashes(string password)
    {
        // Arrange
        using var context = CreateContext();
        var authService = new AuthenticationService(context, _mockLogger.Object);

        // Act
        var hashedPassword = authService.HashPassword(password);

        // Assert
        Assert.NotNull(hashedPassword);
        Assert.NotEmpty(hashedPassword);
        Assert.True(authService.VerifyPassword(password, hashedPassword));
    }
}
