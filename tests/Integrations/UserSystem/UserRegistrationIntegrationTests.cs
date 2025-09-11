using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DbApp.Application.UserSystem.Visitors;
using DbApp.Application.UserSystem.Employees;
using DbApp.Domain.Entities.UserSystem;
using DbApp.Domain.Enums.UserSystem;
using DbApp.Infrastructure.Services.UserSystem;
using DbApp.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace DbApp.Tests.Integrations.UserSystem;

/// <summary>
/// 用户注册集成测试
/// </summary>
[Collection("Database")]
public class UserRegistrationIntegrationTests(DatabaseFixture fixture) : IAsyncLifetime
{
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly List<int> _createdUserIds = new();
    private readonly List<int> _createdRoleIds = new();

    public async Task InitializeAsync()
    {
        await EnsureTestRolesExist();
    }

    public async Task DisposeAsync()
    {
        await CleanupTestData();
    }

    private async Task EnsureTestRolesExist()
    {
        var db = fixture.DbContext;

        // 确保访客角色存在
        var visitorRole = await db.Roles.FirstOrDefaultAsync(r => r.RoleName == "Visitor");
        if (visitorRole == null)
        {
            visitorRole = new Role
            {
                RoleName = "Visitor",
                IsSystemRole = true,
                CreatedAt = DateTime.UtcNow
            };
            db.Roles.Add(visitorRole);
            await db.SaveChangesAsync();
            _createdRoleIds.Add(visitorRole.RoleId);
        }

        // 确保员工角色存在
        var employeeRole = await db.Roles.FirstOrDefaultAsync(r => r.RoleName == "Employee");
        if (employeeRole == null)
        {
            employeeRole = new Role
            {
                RoleName = "Employee",
                IsSystemRole = true,
                CreatedAt = DateTime.UtcNow
            };
            db.Roles.Add(employeeRole);
            await db.SaveChangesAsync();
            _createdRoleIds.Add(employeeRole.RoleId);
        }

        Console.WriteLine("✓ Test roles ensured");
    }

    private async Task CleanupTestData()
    {
        var db = fixture.DbContext;

        // 清理创建的用户
        foreach (var userId in _createdUserIds)
        {
            var user = await db.Users.FirstOrDefaultAsync(u => u.UserId == userId);
            if (user != null)
            {
                // 先删除相关的访客或员工记录
                var visitor = await db.Visitors.FirstOrDefaultAsync(v => v.VisitorId == userId);
                if (visitor != null)
                {
                    db.Visitors.Remove(visitor);
                }

                var employee = await db.Employees.FirstOrDefaultAsync(e => e.EmployeeId == userId);
                if (employee != null)
                {
                    db.Employees.Remove(employee);
                }

                db.Users.Remove(user);
            }
        }

        // 清理创建的角色
        foreach (var roleId in _createdRoleIds)
        {
            var role = await db.Roles.FirstOrDefaultAsync(r => r.RoleId == roleId);
            if (role != null)
            {
                db.Roles.Remove(role);
            }
        }

        await db.SaveChangesAsync();
        Console.WriteLine("✓ Cleaned up test registration data");
    }

    [Fact]
    public async Task CreateVisitor_WithValidData_ShouldReturnSuccess()
    {
        // Arrange
        using var factory = new TestApiFactory(fixture);
        using var client = factory.CreateClient();

        var logger = new Mock<ILogger<AuthenticationService>>().Object;
        var authService = new AuthenticationService(fixture.DbContext, logger);
        var hashedPassword = authService.HashPassword("testpassword123");

        var createCommand = new CreateVisitorCommand(
            Username: $"testvisitor_{Guid.NewGuid():N}",
            PasswordHash: hashedPassword,
            Email: "visitor@example.com",
            DisplayName: "Test Visitor",
            PhoneNumber: "1234567890",
            BirthDate: new DateTime(1990, 1, 1),
            Gender: Gender.Male,
            VisitorType: VisitorType.Regular,
            Height: 175
        );

        // Act
        var response = await client.PostAsJsonAsync("/api/user/visitors", createCommand, _jsonOptions);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        // 验证数据库中的数据
        var db = fixture.DbContext;
        var createdUser = await db.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Username == createCommand.Username);

        Assert.NotNull(createdUser);
        Assert.Equal(createCommand.DisplayName, createdUser.DisplayName);
        Assert.Equal(createCommand.Email, createdUser.Email);
        Assert.Equal(createCommand.PhoneNumber, createdUser.PhoneNumber);

        var createdVisitor = await db.Visitors
            .FirstOrDefaultAsync(v => v.VisitorId == createdUser.UserId);

        Assert.NotNull(createdVisitor);
        Assert.Equal(createCommand.Height, createdVisitor.Height);
        Assert.Equal(createCommand.VisitorType, createdVisitor.VisitorType);
        Assert.Equal(0, createdVisitor.Points);
        Assert.False(createdVisitor.IsBlacklisted);

        _createdUserIds.Add(createdUser.UserId);
        Console.WriteLine($"✓ Visitor created successfully: {createdUser.Username}");
    }

    [Fact]
    public async Task CreateVisitor_MemberWithoutContact_ShouldReturnBadRequest()
    {
        // Arrange
        using var factory = new TestApiFactory(fixture);
        using var client = factory.CreateClient();

        var logger = new Mock<ILogger<AuthenticationService>>().Object;
        var authService = new AuthenticationService(fixture.DbContext, logger);
        var hashedPassword = authService.HashPassword("testpassword123");

        var createCommand = new CreateVisitorCommand(
            Username: $"testmember_{Guid.NewGuid():N}",
            PasswordHash: hashedPassword,
            Email: null, // 会员必须有联系方式
            DisplayName: "Test Member",
            PhoneNumber: null, // 会员必须有联系方式
            BirthDate: new DateTime(1990, 1, 1),
            Gender: Gender.Male,
            VisitorType: VisitorType.Member,
            Height: 175
        );

        // Act
        var response = await client.PostAsJsonAsync("/api/user/visitors", createCommand, _jsonOptions);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var jsonContent = await response.Content.ReadAsStringAsync();
        Assert.Contains("会员账户必须提供邮箱或电话号码中的至少一项", jsonContent);

        Console.WriteLine("✓ Member registration correctly rejected without contact info");
    }

    [Theory]
    [InlineData("member@example.com", null)] // 只有邮箱
    [InlineData(null, "1234567890")] // 只有电话
    [InlineData("member@example.com", "1234567890")] // 两者都有
    public async Task CreateVisitor_MemberWithValidContact_ShouldReturnSuccess(string? email, string? phoneNumber)
    {
        // Arrange
        using var factory = new TestApiFactory(fixture);
        using var client = factory.CreateClient();

        var logger = new Mock<ILogger<AuthenticationService>>().Object;
        var authService = new AuthenticationService(fixture.DbContext, logger);
        var hashedPassword = authService.HashPassword("testpassword123");

        var createCommand = new CreateVisitorCommand(
            Username: $"testmember_{Guid.NewGuid():N}",
            PasswordHash: hashedPassword,
            Email: email,
            DisplayName: "Test Member",
            PhoneNumber: phoneNumber,
            BirthDate: new DateTime(1990, 1, 1),
            Gender: Gender.Male,
            VisitorType: VisitorType.Member,
            Height: 175
        );

        // Act
        var response = await client.PostAsJsonAsync("/api/user/visitors", createCommand, _jsonOptions);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        // 验证数据库中的数据
        var db = fixture.DbContext;
        var createdUser = await db.Users
            .FirstOrDefaultAsync(u => u.Username == createCommand.Username);

        Assert.NotNull(createdUser);
        Assert.Equal(email, createdUser.Email);
        Assert.Equal(phoneNumber, createdUser.PhoneNumber);

        var createdVisitor = await db.Visitors
            .FirstOrDefaultAsync(v => v.VisitorId == createdUser.UserId);

        Assert.NotNull(createdVisitor);
        Assert.Equal(VisitorType.Member, createdVisitor.VisitorType);

        _createdUserIds.Add(createdUser.UserId);
        Console.WriteLine($"✓ Member created successfully: {createdUser.Username}");
    }

    [Fact]
    public async Task CreateEmployee_WithValidData_ShouldReturnSuccess()
    {
        // Arrange
        using var factory = new TestApiFactory(fixture);
        using var client = factory.CreateClient();

        var logger = new Mock<ILogger<AuthenticationService>>().Object;
        var authService = new AuthenticationService(fixture.DbContext, logger);
        var hashedPassword = authService.HashPassword("testpassword123");

        var createCommand = new CreateEmployeeCommand(
            Username: $"testemployee_{Guid.NewGuid():N}",
            PasswordHash: hashedPassword,
            Email: "employee@company.com",
            DisplayName: "Test Employee",
            PhoneNumber: "1234567890",
            BirthDate: new DateTime(1985, 5, 15),
            EmployeeType: EmployeeType.Employee,
            StaffNumber: $"EMP{DateTime.Now.Ticks}",
            Position: "Software Developer",
            DepartmentName: "IT",
            TeamId: null,
            ManagerId: null,
            Certification: "Microsoft Certified",
            ResponsibilityArea: "Backend Development",
            StaffType: StaffType.Regular
        );

        // Act
        var response = await client.PostAsJsonAsync("/api/user/employees", createCommand, _jsonOptions);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        // 验证数据库中的数据
        var db = fixture.DbContext;
        var createdUser = await db.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Username == createCommand.Username);

        Assert.NotNull(createdUser);
        Assert.Equal(createCommand.DisplayName, createdUser.DisplayName);
        Assert.Equal(createCommand.Email, createdUser.Email);
        Assert.Equal(createCommand.PhoneNumber, createdUser.PhoneNumber);

        var createdEmployee = await db.Employees
            .FirstOrDefaultAsync(e => e.EmployeeId == createdUser.UserId);

        Assert.NotNull(createdEmployee);
        Assert.Equal(createCommand.StaffNumber, createdEmployee.StaffNumber);
        Assert.Equal(createCommand.Position, createdEmployee.Position);
        Assert.Equal(createCommand.DepartmentName, createdEmployee.DepartmentName);
        Assert.Equal(createCommand.Certification, createdEmployee.Certification);
        Assert.Equal(createCommand.ResponsibilityArea, createdEmployee.ResponsibilityArea);

        _createdUserIds.Add(createdUser.UserId);
        Console.WriteLine($"✓ Employee created successfully: {createdUser.Username}");
    }

    [Fact]
    public async Task CreateVisitor_WithDuplicateUsername_ShouldReturnConflict()
    {
        // Arrange
        using var factory = new TestApiFactory(fixture);
        using var client = factory.CreateClient();

        var logger = new Mock<ILogger<AuthenticationService>>().Object;
        var authService = new AuthenticationService(fixture.DbContext, logger);
        var hashedPassword = authService.HashPassword("testpassword123");

        var username = $"duplicateuser_{Guid.NewGuid():N}";

        var firstCommand = new CreateVisitorCommand(
            Username: username,
            PasswordHash: hashedPassword,
            Email: "first@example.com",
            DisplayName: "First User",
            PhoneNumber: "1234567890",
            BirthDate: new DateTime(1990, 1, 1),
            Gender: Gender.Male,
            VisitorType: VisitorType.Regular,
            Height: 175
        );

        var secondCommand = new CreateVisitorCommand(
            Username: username, // 相同的用户名
            PasswordHash: hashedPassword,
            Email: "second@example.com",
            DisplayName: "Second User",
            PhoneNumber: "0987654321",
            BirthDate: new DateTime(1991, 2, 2),
            Gender: Gender.Female,
            VisitorType: VisitorType.Regular,
            Height: 165
        );

        // Act
        var firstResponse = await client.PostAsJsonAsync("/api/user/visitors", firstCommand, _jsonOptions);
        var secondResponse = await client.PostAsJsonAsync("/api/user/visitors", secondCommand, _jsonOptions);

        // Assert
        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, secondResponse.StatusCode);

        // 记录第一个用户以便清理
        var db = fixture.DbContext;
        var createdUser = await db.Users.FirstOrDefaultAsync(u => u.Username == username);
        if (createdUser != null)
        {
            _createdUserIds.Add(createdUser.UserId);
        }

        Console.WriteLine("✓ Duplicate username correctly rejected");
    }

    [Theory]
    [InlineData("", "hashedpassword", "email@test.com", "Display Name")]
    [InlineData("username", "", "email@test.com", "Display Name")]
    [InlineData("username", "hashedpassword", "", "Display Name")]
    [InlineData("username", "hashedpassword", "email@test.com", "")]
    public async Task CreateVisitor_WithMissingRequiredFields_ShouldReturnBadRequest(
        string username, string passwordHash, string email, string displayName)
    {
        // Arrange
        using var factory = new TestApiFactory(fixture);
        using var client = factory.CreateClient();

        var createCommand = new CreateVisitorCommand(
            Username: username,
            PasswordHash: passwordHash,
            Email: email,
            DisplayName: displayName,
            PhoneNumber: null,
            BirthDate: null,
            Gender: null,
            VisitorType: VisitorType.Regular,
            Height: 175
        );

        // Act
        var response = await client.PostAsJsonAsync("/api/user/visitors", createCommand, _jsonOptions);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        Console.WriteLine("✓ Registration correctly rejected missing required fields");
    }
}
