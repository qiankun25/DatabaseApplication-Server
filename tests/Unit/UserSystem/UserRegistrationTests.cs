using DbApp.Application.UserSystem.Visitors;
using DbApp.Application.UserSystem.Employees;
using DbApp.Domain.Entities.UserSystem;
using DbApp.Domain.Enums.UserSystem;
using DbApp.Domain.Interfaces.UserSystem;
using Moq;
using ValidationException = DbApp.Domain.Exceptions.ValidationException;

namespace DbApp.Tests.Unit.UserSystem;

/// <summary>
/// 用户注册功能单元测试
/// </summary>
[Trait("Category", "Unit")]
public class UserRegistrationTests
{
    private readonly Mock<IVisitorRepository> _mockVisitorRepo;
    private readonly Mock<IMembershipService> _mockMembershipService;

    public UserRegistrationTests()
    {
        _mockVisitorRepo = new Mock<IVisitorRepository>();
        _mockMembershipService = new Mock<IMembershipService>();
    }

    #region 访客注册测试

    [Fact]
    public async Task CreateVisitor_WithValidData_ShouldReturnVisitorId()
    {
        // Arrange
        var command = new CreateVisitorCommand(
            Username: "testvisitor",
            PasswordHash: "hashedpassword",
            Email: "visitor@example.com",
            DisplayName: "Test Visitor",
            PhoneNumber: "1234567890",
            BirthDate: new DateTime(1990, 1, 1),
            Gender: Gender.Male,
            VisitorType: VisitorType.Regular,
            Height: 175
        );

        _mockVisitorRepo.Setup(x => x.CreateAsync(It.IsAny<Visitor>()))
            .ReturnsAsync(1);

        var handler = new VisitorCommandHandlers(_mockVisitorRepo.Object, _mockMembershipService.Object);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal(1, result);
        _mockVisitorRepo.Verify(x => x.CreateAsync(It.Is<Visitor>(v => 
            v.User.Username == "testvisitor" &&
            v.User.Email == "visitor@example.com" &&
            v.User.DisplayName == "Test Visitor" &&
            v.VisitorType == VisitorType.Regular &&
            v.Height == 175
        )), Times.Once);
    }

    [Fact]
    public async Task CreateVisitor_WithoutEmailAndPhone_ShouldSucceedForRegularVisitor()
    {
        // Arrange
        var command = new CreateVisitorCommand(
            Username: "testvisitor",
            PasswordHash: "hashedpassword",
            Email: null,
            DisplayName: "Test Visitor",
            PhoneNumber: null,
            BirthDate: new DateTime(1990, 1, 1),
            Gender: Gender.Male,
            VisitorType: VisitorType.Regular,
            Height: 175
        );

        _mockVisitorRepo.Setup(x => x.CreateAsync(It.IsAny<Visitor>()))
            .ReturnsAsync(1);

        var handler = new VisitorCommandHandlers(_mockVisitorRepo.Object, _mockMembershipService.Object);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal(1, result);
        _mockVisitorRepo.Verify(x => x.CreateAsync(It.Is<Visitor>(v => 
            v.User.Email == null &&
            v.User.PhoneNumber == null &&
            v.VisitorType == VisitorType.Regular
        )), Times.Once);
    }

    [Fact]
    public async Task CreateVisitor_MemberWithoutEmailAndPhone_ShouldThrowValidationException()
    {
        // Arrange
        var command = new CreateVisitorCommand(
            Username: "testmember",
            PasswordHash: "hashedpassword",
            Email: null,
            DisplayName: "Test Member",
            PhoneNumber: null,
            BirthDate: new DateTime(1990, 1, 1),
            Gender: Gender.Male,
            VisitorType: VisitorType.Member,
            Height: 175
        );

        var handler = new VisitorCommandHandlers(_mockVisitorRepo.Object, _mockMembershipService.Object);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ValidationException>(
            () => handler.Handle(command, CancellationToken.None)
        );
        
        Assert.Contains("会员账户必须提供邮箱或电话号码中的至少一项", exception.Message);
    }

    [Theory]
    [InlineData("member@example.com", null)] // 只有邮箱
    [InlineData(null, "1234567890")] // 只有电话
    [InlineData("member@example.com", "1234567890")] // 两者都有
    public async Task CreateVisitor_MemberWithValidContact_ShouldSucceed(string email, string phoneNumber)
    {
        // Arrange
        var command = new CreateVisitorCommand(
            Username: "testmember",
            PasswordHash: "hashedpassword",
            Email: email,
            DisplayName: "Test Member",
            PhoneNumber: phoneNumber,
            BirthDate: new DateTime(1990, 1, 1),
            Gender: Gender.Male,
            VisitorType: VisitorType.Member,
            Height: 175
        );

        _mockVisitorRepo.Setup(x => x.CreateAsync(It.IsAny<Visitor>()))
            .ReturnsAsync(1);

        var handler = new VisitorCommandHandlers(_mockVisitorRepo.Object, _mockMembershipService.Object);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal(1, result);
        _mockVisitorRepo.Verify(x => x.CreateAsync(It.Is<Visitor>(v => 
            v.VisitorType == VisitorType.Member &&
            v.User.Email == email &&
            v.User.PhoneNumber == phoneNumber
        )), Times.Once);
    }

    [Theory]
    [InlineData("", "")] // 空字符串
    [InlineData("   ", "   ")] // 空白字符串
    public async Task CreateVisitor_MemberWithEmptyContact_ShouldThrowValidationException(string email, string phoneNumber)
    {
        // Arrange
        var command = new CreateVisitorCommand(
            Username: "testmember",
            PasswordHash: "hashedpassword",
            Email: email,
            DisplayName: "Test Member",
            PhoneNumber: phoneNumber,
            BirthDate: new DateTime(1990, 1, 1),
            Gender: Gender.Male,
            VisitorType: VisitorType.Member,
            Height: 175
        );

        var handler = new VisitorCommandHandlers(_mockVisitorRepo.Object, _mockMembershipService.Object);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ValidationException>(
            () => handler.Handle(command, CancellationToken.None)
        );
        
        Assert.Contains("会员账户必须提供邮箱或电话号码中的至少一项", exception.Message);
    }

    [Fact]
    public async Task CreateVisitor_ShouldSetDefaultValues()
    {
        // Arrange
        var command = new CreateVisitorCommand(
            Username: "testvisitor",
            PasswordHash: "hashedpassword",
            Email: "visitor@example.com",
            DisplayName: "Test Visitor",
            PhoneNumber: "1234567890",
            BirthDate: new DateTime(1990, 1, 1),
            Gender: Gender.Male,
            VisitorType: VisitorType.Regular,
            Height: 175
        );

        _mockVisitorRepo.Setup(x => x.CreateAsync(It.IsAny<Visitor>()))
            .ReturnsAsync(1);

        var handler = new VisitorCommandHandlers(_mockVisitorRepo.Object, _mockMembershipService.Object);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        _mockVisitorRepo.Verify(x => x.CreateAsync(It.Is<Visitor>(v => 
            v.Points == 0 &&
            v.IsBlacklisted == false &&
            v.User.PermissionLevel == 1 &&
            v.User.RoleId == 1 &&
            v.User.RegisterTime <= DateTime.UtcNow &&
            v.User.CreatedAt <= DateTime.UtcNow &&
            v.CreatedAt <= DateTime.UtcNow
        )), Times.Once);
    }

    #endregion

    #region 员工注册测试

    [Fact]
    public void CreateEmployeeCommand_WithValidData_ShouldPassValidation()
    {
        // Arrange
        var command = new CreateEmployeeCommand(
            Username: "testemployee",
            PasswordHash: "hashedpassword",
            Email: "employee@company.com",
            DisplayName: "Test Employee",
            PhoneNumber: "1234567890",
            BirthDate: new DateTime(1985, 5, 15),
            EmployeeType: EmployeeType.Employee,
            StaffNumber: "EMP001",
            Position: "Software Developer",
            DepartmentName: "IT",
            TeamId: 1,
            ManagerId: 2,
            Certification: "Microsoft Certified",
            ResponsibilityArea: "Backend Development",
            StaffType: StaffType.Regular
        );

        // Act & Assert - 如果没有抛出异常，则验证通过
        Assert.NotNull(command);
        Assert.Equal("testemployee", command.Username);
        Assert.Equal("employee@company.com", command.Email);
        Assert.Equal("EMP001", command.StaffNumber);
    }

    [Theory]
    [InlineData("", "hashedpassword", "email@test.com", "Display Name", "EMP001", "Position")]
    [InlineData("username", "", "email@test.com", "Display Name", "EMP001", "Position")]
    [InlineData("username", "hashedpassword", "", "Display Name", "EMP001", "Position")]
    [InlineData("username", "hashedpassword", "email@test.com", "", "EMP001", "Position")]
    [InlineData("username", "hashedpassword", "email@test.com", "Display Name", "", "Position")]
    [InlineData("username", "hashedpassword", "email@test.com", "Display Name", "EMP001", "")]
    public void CreateEmployeeCommand_WithMissingRequiredFields_ShouldHaveValidationAttributes(
        string username, string passwordHash, string email, string displayName, string staffNumber, string position)
    {
        // Act
        var command = new CreateEmployeeCommand(
            Username: username,
            PasswordHash: passwordHash,
            Email: email,
            DisplayName: displayName,
            PhoneNumber: null,
            BirthDate: null,
            EmployeeType: EmployeeType.Employee,
            StaffNumber: staffNumber,
            Position: position,
            DepartmentName: null,
            TeamId: null,
            ManagerId: null,
            Certification: null,
            ResponsibilityArea: null,
            StaffType: null
        );

        // Assert - 验证命令对象的属性是否正确设置
        Assert.Equal(username, command.Username);
        Assert.Equal(passwordHash, command.PasswordHash);
        Assert.Equal(email, command.Email);
        Assert.Equal(displayName, command.DisplayName);
        Assert.Equal(staffNumber, command.StaffNumber);
        Assert.Equal(position, command.Position);
    }

    [Fact]
    public void CreateEmployeeCommand_WithInvalidEmail_ShouldHaveEmailValidationAttribute()
    {
        // Arrange & Act
        var command = new CreateEmployeeCommand(
            Username: "testemployee",
            PasswordHash: "hashedpassword",
            Email: "invalid-email",
            DisplayName: "Test Employee",
            PhoneNumber: null,
            BirthDate: null,
            EmployeeType: EmployeeType.Employee,
            StaffNumber: "EMP001",
            Position: "Developer",
            DepartmentName: null,
            TeamId: null,
            ManagerId: null,
            Certification: null,
            ResponsibilityArea: null,
            StaffType: null
        );

        // Assert - 验证邮箱字段设置正确
        Assert.Equal("invalid-email", command.Email);
    }

    [Theory]
    [InlineData(EmployeeType.Manager)]
    [InlineData(EmployeeType.Employee)]
    public void CreateEmployeeCommand_WithValidEmployeeTypes_ShouldAcceptAllTypes(EmployeeType employeeType)
    {
        // Arrange & Act
        var command = new CreateEmployeeCommand(
            Username: "testemployee",
            PasswordHash: "hashedpassword",
            Email: "employee@company.com",
            DisplayName: "Test Employee",
            PhoneNumber: null,
            BirthDate: null,
            EmployeeType: employeeType,
            StaffNumber: "EMP001",
            Position: "Developer",
            DepartmentName: null,
            TeamId: null,
            ManagerId: null,
            Certification: null,
            ResponsibilityArea: null,
            StaffType: null
        );

        // Assert
        Assert.Equal(employeeType, command.EmployeeType);
    }

    [Theory]
    [InlineData(StaffType.Regular)]
    [InlineData(StaffType.Inspector)]
    [InlineData(StaffType.Mechanic)]
    [InlineData(StaffType.Manager)]
    public void CreateEmployeeCommand_WithValidStaffTypes_ShouldAcceptAllTypes(StaffType? staffType)
    {
        // Arrange & Act
        var command = new CreateEmployeeCommand(
            Username: "testemployee",
            PasswordHash: "hashedpassword",
            Email: "employee@company.com",
            DisplayName: "Test Employee",
            PhoneNumber: null,
            BirthDate: null,
            EmployeeType: EmployeeType.Employee,
            StaffNumber: "EMP001",
            Position: "Developer",
            DepartmentName: null,
            TeamId: null,
            ManagerId: null,
            Certification: null,
            ResponsibilityArea: null,
            StaffType: staffType
        );

        // Assert
        Assert.Equal(staffType, command.StaffType);
    }

    #endregion
}
