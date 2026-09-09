using Electronic_Election_Management_System.Data.Repositories;
using Electronic_Election_Management_System.DTOs;
using Electronic_Election_Management_System.Models;
using Electronic_Election_Management_System.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Electronic_Election_Management_System.Tests.Services;

/// <summary>
/// The profile read and write paths on <see cref="UserService"/>. Both hand-map every field, so
/// a column added to the entity stays invisible to the client until it is added here too.
/// </summary>
public class UserDetailsServiceTests
{
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IAuditLogRepository _auditLogs = Substitute.For<IAuditLogRepository>();
    private readonly IUserNotifier _notifier = Substitute.For<IUserNotifier>();
    private readonly ICnpService _cnp = Substitute.For<ICnpService>();
    private readonly ILogger<UserService> _logger = Substitute.For<ILogger<UserService>>();
    private readonly UserService _service;

    public UserDetailsServiceTests()
    {
        _service = new UserService(_users, _auditLogs, _notifier, _cnp, _logger);
    }

    [Fact]
    public async Task GetMyDetailsAsync_ReturnsTheSavedResidenceCountry()
    {
        var userId = Guid.NewGuid();
        _users.GetUserDetailsAsync(userId).Returns(new UserDetails
        {
            UserId = userId,
            ResidenceCountry = "RO",
            ResidenceCountyCode = "RO-CJ"
        });

        var dto = await _service.GetMyDetailsAsync(userId);

        dto!.ResidenceCountry.Should().Be("RO");
        dto.ResidenceCountyCode.Should().Be("RO-CJ");
    }

    [Fact]
    public async Task SaveMyDetailsAsync_ReturnsTheResidenceCountryThatWasPersisted()
    {
        var userId = Guid.NewGuid();
        _users.SaveUserDetailsAsync(userId, Arg.Any<PersonalDetailsDto>())
            .Returns(new UserDetails
            {
                UserId = userId, ResidenceCountry = "RO", ResidenceCountyCode = "RO-CJ"
            });

        var result = await _service.SaveMyDetailsAsync(userId, new PersonalDetailsDto
        {
            ResidenceCountry = "RO"
        });

        result.Success.Should().BeTrue();
        result.Data!.ResidenceCountry.Should().Be("RO");
        result.Data.ResidenceCountyCode.Should().Be("RO-CJ");
    }
}
