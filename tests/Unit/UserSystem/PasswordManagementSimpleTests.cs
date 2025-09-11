using DbApp.Application.UserSystem.Authentication;
using DbApp.Domain.Entities.UserSystem;
using DbApp.Domain.Services.UserSystem;
using DbApp.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using System.ComponentModel.DataAnnotations;
using ValidationException = DbApp.Application.Common.Exceptions.ValidationException;

namespace DbApp.Tests.Unit.UserSystem;

/// <summary>
/// 简化的密码管理功能单元测试
/// </summary>
[Trait("Category", "Unit")]
public class PasswordManagementSimpleTests
{
    private readonly Mock<ILogger<ChangePasswordCommandHandler>> _mockLogger;
    private readonly Mock<IAuthenticationService> _mockAuthService;
    private readonly Mock<ICacheService> _mockCacheService;
    private readonly DbContextOptions<ApplicationDbContext> _dbOptions;

    public PasswordManagementSimpleTests()
    {
        _mockLogger = new Mock<ILogger<ChangePasswordCommandHandler>>();
        _mockAuthService = new Mock<IAuthenticationService>();
        _mockCacheService = new Mock<ICacheService>();
        
        _dbOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
    }

    private ApplicationDbContext CreateContext()
    {
        return new ApplicationDbContext(_dbOptions);
    }

    private async Task<User> CreateTestUserAsync(ApplicationDbContext context)
    {
        var role = new Role
        {
            RoleName = "TestRole",
            IsSystemRole = true,
            CreatedAt = DateTime.UtcNow
        };
        context.Roles.Add(role);
        await context.SaveChangesAsync();

        var user = new User
        {
            UserId = 1,
            Username = "testuser",
            PasswordHash = "currenthashedpassword",
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
    public async Task ChangePassword_WithIncorrectCurrentPassword_ShouldThrowValidationException()
    {
        // Arrange
        using var context = CreateContext();
        var testUser = await CreateTestUserAsync(context);

        var command = new ChangePasswordCommand(
            UserId: testUser.UserId,
            CurrentPassword: "wrongcurrentpassword",
            NewPassword: "newpassword123",
            ConfirmPassword: "newpassword123"
        );

        _mockAuthService.Setup(x => x.VerifyPassword("wrongcurrentpassword", "currenthashedpassword"))
            .Returns(false);

        var handler = new ChangePasswordCommandHandler(
            context,
            _mockAuthService.Object,
            _mockCacheService.Object,
            _mockLogger.Object
        );

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ValidationException>(
            () => handler.Handle(command, CancellationToken.None)
        );

        Assert.Equal("Current password is incorrect.", exception.Message);
    }

    [Fact]
    public async Task ChangePassword_WithMismatchedPasswords_ShouldThrowValidationException()
    {
        // Arrange
        using var context = CreateContext();
        var testUser = await CreateTestUserAsync(context);

        var command = new ChangePasswordCommand(
            UserId: testUser.UserId,
            CurrentPassword: "currentpassword",
            NewPassword: "newpassword123",
            ConfirmPassword: "differentpassword123"
        );

        var handler = new ChangePasswordCommandHandler(
            context,
            _mockAuthService.Object,
            _mockCacheService.Object,
            _mockLogger.Object
        );

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ValidationException>(
            () => handler.Handle(command, CancellationToken.None)
        );

        Assert.Equal("Passwords do not match.", exception.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("12345")] // 少于6个字符
    public async Task ChangePassword_WithInvalidNewPassword_ShouldThrowValidationException(string newPassword)
    {
        // Arrange
        using var context = CreateContext();
        var testUser = await CreateTestUserAsync(context);

        var command = new ChangePasswordCommand(
            UserId: testUser.UserId,
            CurrentPassword: "currentpassword",
            NewPassword: newPassword,
            ConfirmPassword: newPassword
        );

        var handler = new ChangePasswordCommandHandler(
            context,
            _mockAuthService.Object,
            _mockCacheService.Object,
            _mockLogger.Object
        );

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ValidationException>(
            () => handler.Handle(command, CancellationToken.None)
        );

        Assert.Equal("Password must be at least 6 characters long.", exception.Message);
    }

    [Fact]
    public async Task ChangePassword_WithNonexistentUser_ShouldThrowValidationException()
    {
        // Arrange
        using var context = CreateContext();

        var command = new ChangePasswordCommand(
            UserId: 999, // 不存在的用户ID
            CurrentPassword: "currentpassword",
            NewPassword: "newpassword123",
            ConfirmPassword: "newpassword123"
        );

        var handler = new ChangePasswordCommandHandler(
            context,
            _mockAuthService.Object,
            _mockCacheService.Object,
            _mockLogger.Object
        );

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ValidationException>(
            () => handler.Handle(command, CancellationToken.None)
        );

        Assert.Equal("User not found.", exception.Message);
    }

    [Theory]
    [InlineData("password")]
    [InlineData("123456")]
    [InlineData("abcdef")]
    [InlineData("Password123!")]
    [InlineData("VeryLongPasswordWithSpecialCharacters!@#$%^&*()")]
    public async Task ChangePassword_WithVariousValidPasswords_ShouldSucceed(string newPassword)
    {
        // Arrange
        using var context = CreateContext();
        var testUser = await CreateTestUserAsync(context);

        var command = new ChangePasswordCommand(
            UserId: testUser.UserId,
            CurrentPassword: "currentpassword",
            NewPassword: newPassword,
            ConfirmPassword: newPassword
        );

        _mockAuthService.Setup(x => x.VerifyPassword("currentpassword", "currenthashedpassword"))
            .Returns(true);
        _mockAuthService.Setup(x => x.HashPassword(newPassword))
            .Returns($"hashed_{newPassword}");

        var handler = new ChangePasswordCommandHandler(
            context,
            _mockAuthService.Object,
            _mockCacheService.Object,
            _mockLogger.Object
        );

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        var updatedUser = await context.Users.FindAsync(testUser.UserId);
        Assert.NotNull(updatedUser);
        Assert.Equal($"hashed_{newPassword}", updatedUser.PasswordHash);
    }
}
