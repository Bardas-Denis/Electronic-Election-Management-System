using Electronic_Election_Management_System.Constants;
using Electronic_Election_Management_System.Data.Repositories;
using Electronic_Election_Management_System.DTOs;
using Electronic_Election_Management_System.Models;
using Electronic_Election_Management_System.Services;
using FluentAssertions;
using NSubstitute;

namespace Electronic_Election_Management_System.Tests.Services;

public class AuthServiceTests
{
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IAuditLogRepository _auditLogs = Substitute.For<IAuditLogRepository>();
    private readonly ITokenService _tokens = Substitute.For<ITokenService>();
    private readonly AuthService _service;

    public AuthServiceTests()
    {
        _service = new AuthService(_users, _auditLogs, _tokens);
        _tokens.GenerateToken(Arg.Any<User>())
            .Returns(("signed-token", DateTime.UtcNow.AddHours(1)));
    }

    [Fact]
    public async Task RegisterAsync_WithExistingEmail_ReturnsConflictWithoutPersisting()
    {
        _users.ExistsByEmailAsync("voter@example.com").Returns(true);

        var result = await _service.RegisterAsync(new RegisterRequest
        {
            Email = " VOTER@example.com ",
            Password = "secret123"
        });

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCode.EmailAlreadyExists);
        await _users.DidNotReceive().AddAsync(Arg.Any<User>());
        _tokens.DidNotReceive().GenerateToken(Arg.Any<User>());
    }

    [Fact]
    public async Task RegisterAsync_WithValidRequest_NormalizesAndHashesUser()
    {
        User? persistedUser = null;
        _users.AddAsync(Arg.Do<User>(user => persistedUser = user))
            .Returns(Task.CompletedTask);

        var result = await _service.RegisterAsync(new RegisterRequest
        {
            Email = " New.Voter@Example.COM ",
            Password = "secret123"
        });

        result.Success.Should().BeTrue();
        result.Data!.Token.Should().Be("signed-token");
        result.Data.Email.Should().Be("new.voter@example.com");
        result.Data.Role.Should().Be(nameof(UserRole.Voter));
        persistedUser.Should().NotBeNull();
        var savedUser = persistedUser!;
        savedUser.Email.Should().Be("new.voter@example.com");
        savedUser.PasswordHash.Should().NotBe("secret123");
        PasswordHasher.Verify("secret123", savedUser.PasswordHash).Should().BeTrue();
        await _auditLogs.Received(1).AddAsync(Arg.Is<AuditLog>(log =>
            log != null &&
            log.UserId == savedUser.Id &&
            log.Action == AuditAction.AccountCreated.ToDbValue()));
        await _users.Received(1).SaveChangesAsync();
    }

    [Fact]
    public async Task LoginAsync_WithInvalidCredentials_DoesNotRevealWhichValueFailed()
    {
        _users.GetByEmailAsync("missing@example.com").Returns((User?)null);

        var result = await _service.LoginAsync(new LoginRequest
        {
            Email = "missing@example.com",
            Password = "wrong"
        });

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCode.InvalidCredentials);
        await _auditLogs.DidNotReceive().AddAsync(Arg.Any<AuditLog>());
        _tokens.DidNotReceive().GenerateToken(Arg.Any<User>());
    }

    [Fact]
    public async Task LoginAsync_WithValidCredentials_ReturnsTokenAndAuditsLogin()
    {
        var user = new User
        {
            Email = "manager@example.com",
            PasswordHash = PasswordHasher.Hash("correct-password"),
            Role = UserRole.ElectionManager
        };
        _users.GetByEmailAsync(user.Email).Returns(user);

        var result = await _service.LoginAsync(new LoginRequest
        {
            Email = " MANAGER@example.com ",
            Password = "correct-password"
        });

        result.Success.Should().BeTrue();
        result.Data!.Token.Should().Be("signed-token");
        result.Data.Role.Should().Be(nameof(UserRole.ElectionManager));
        await _auditLogs.Received(1).AddAsync(Arg.Is<AuditLog>(log =>
            log != null &&
            log.UserId == user.Id &&
            log.Action == AuditAction.Login.ToDbValue()));
        await _auditLogs.Received(1).SaveChangesAsync();
    }
}
