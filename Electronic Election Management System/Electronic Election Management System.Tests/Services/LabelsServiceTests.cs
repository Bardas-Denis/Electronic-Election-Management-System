using Electronic_Election_Management_System.Constants;
using Electronic_Election_Management_System.Data.Repositories;
using Electronic_Election_Management_System.DTOs;
using Electronic_Election_Management_System.Models;
using Electronic_Election_Management_System.Services;
using FluentAssertions;
using NSubstitute;

namespace Electronic_Election_Management_System.Tests.Services;

public class LabelServiceTests
{
    private readonly ILabelRepository _labels = Substitute.For<ILabelRepository>();
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly LabelService _service;

    public LabelServiceTests()
    {
        _service = new LabelService(_labels, _users);
    }

    [Fact]
    public async Task GetAllLabelsAsync_MapsRepositoryLabels()
    {
        var createdAt = DateTime.UtcNow.AddDays(-1);
        var label = new Label
        {
            Id = Guid.NewGuid(),
            Name = "Siemens",
            Category = "Employer",
            CreatedAt = createdAt
        };
        _labels.GetAllAsync().Returns([label]);

        var result = await _service.GetAllLabelsAsync();

        result.Should().ContainSingle().Which.Should().BeEquivalentTo(new LabelDto
        {
            Id = label.Id,
            Name = label.Name,
            Category = label.Category,
            CreatedAt = createdAt
        });
    }

    [Fact]
    public async Task CreateLabelAsync_WithDuplicateName_ReturnsErrorWithoutPersisting()
    {
        _labels.ExistsByNameAsync("Siemens").Returns(true);

        var result = await _service.CreateLabelAsync(new CreateLabelRequest
        {
            Name = " Siemens ",
            Category = " Employer "
        });

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCode.LabelNameAlreadyExists);
        await _labels.DidNotReceive().AddAsync(Arg.Any<Label>());
        await _labels.DidNotReceive().SaveChangesAsync();
    }

    [Fact]
    public async Task CreateLabelAsync_WithValidRequest_TrimsAndPersistsLabel()
    {
        Label? persistedLabel = null;
        _labels.ExistsByNameAsync("Siemens").Returns(false);
        _labels.AddAsync(Arg.Do<Label>(label => persistedLabel = label))
            .Returns(Task.CompletedTask);

        var result = await _service.CreateLabelAsync(new CreateLabelRequest
        {
            Name = " Siemens ",
            Category = " Employer "
        });

        result.Success.Should().BeTrue();
        persistedLabel.Should().NotBeNull();
        persistedLabel!.Name.Should().Be("Siemens");
        persistedLabel.Category.Should().Be("Employer");
        result.Data.Should().BeEquivalentTo(new
        {
            persistedLabel.Id,
            persistedLabel.Name,
            persistedLabel.Category,
            persistedLabel.CreatedAt
        });
        await _labels.Received(1).SaveChangesAsync();
    }

    [Fact]
    public async Task DeleteLabelAsync_WhenLabelDoesNotExist_ReturnsNotFound()
    {
        var labelId = Guid.NewGuid();
        _labels.GetByIdAsync(labelId).Returns((Label?)null);

        var result = await _service.DeleteLabelAsync(labelId);

        result.Success.Should().BeFalse();
        result.IsNotFound.Should().BeTrue();
        result.ErrorCode.Should().Be(ErrorCode.LabelNotFound);
        _labels.DidNotReceive().Remove(Arg.Any<Label>());
        await _labels.DidNotReceive().SaveChangesAsync();
    }

    [Fact]
    public async Task DeleteLabelAsync_WhenLabelExists_RemovesAndSaves()
    {
        var label = new Label { Id = Guid.NewGuid(), Name = "Siemens" };
        _labels.GetByIdAsync(label.Id).Returns(label);

        var result = await _service.DeleteLabelAsync(label.Id);

        result.Success.Should().BeTrue();
        result.Data.Should().BeTrue();
        _labels.Received(1).Remove(label);
        await _labels.Received(1).SaveChangesAsync();
    }

    [Fact]
    public async Task GetUserLabelsAsync_WhenUserDoesNotExist_ReturnsNotFound()
    {
        var userId = Guid.NewGuid();
        _users.GetByIdAsync(userId).Returns((User?)null);

        var result = await _service.GetUserLabelsAsync(userId);

        result.Success.Should().BeFalse();
        result.IsNotFound.Should().BeTrue();
        result.ErrorCode.Should().Be(ErrorCode.ResourceNotFound);
        await _labels.DidNotReceive().GetUserLabelsAsync(Arg.Any<Guid>());
    }

    [Fact]
    public async Task GetUserLabelsAsync_WhenUserExists_MapsAssignmentMetadata()
    {
        var user = new User { Id = Guid.NewGuid(), Email = "voter@example.com" };
        var assignment = CreateAssignment(user);
        _users.GetByIdAsync(user.Id).Returns(user);
        _labels.GetUserLabelsAsync(user.Id).Returns([assignment]);

        var result = await _service.GetUserLabelsAsync(user.Id);

        result.Success.Should().BeTrue();
        result.Data.Should().ContainSingle().Which.Should().BeEquivalentTo(new UserLabelDto
        {
            LabelId = assignment.LabelId,
            Name = assignment.Label.Name,
            Category = assignment.Label.Category,
            AssignedBy = assignment.AssignedBy,
            AssignedAt = assignment.AssignedAt
        });
    }

    [Fact]
    public async Task AssignLabelsToUserAsync_WhenUserDoesNotExist_StopsBeforeLabelValidation()
    {
        var userId = Guid.NewGuid();
        _users.GetByIdAsync(userId).Returns((User?)null);

        var result = await _service.AssignLabelsToUserAsync(
            userId,
            new AssignLabelsRequest { LabelIds = [Guid.NewGuid()] },
            Guid.NewGuid());

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCode.ResourceNotFound);
        await _labels.DidNotReceive().GetByIdsAsync(Arg.Any<IEnumerable<Guid>>());
        await _labels.DidNotReceive().AssignLabelsAsync(
            Arg.Any<Guid>(),
            Arg.Any<IEnumerable<Guid>>(),
            Arg.Any<Guid>());
    }

    [Fact]
    public async Task AssignLabelsToUserAsync_WhenAnyLabelIsMissing_ReturnsErrorWithoutAssigning()
    {
        var user = new User { Id = Guid.NewGuid(), Email = "voter@example.com" };
        var existingLabel = new Label { Id = Guid.NewGuid(), Name = "Existing" };
        var missingLabelId = Guid.NewGuid();
        _users.GetByIdAsync(user.Id).Returns(user);
        _labels.GetByIdsAsync(Arg.Any<IEnumerable<Guid>>()).Returns([existingLabel]);

        var result = await _service.AssignLabelsToUserAsync(
            user.Id,
            new AssignLabelsRequest { LabelIds = [existingLabel.Id, missingLabelId] },
            Guid.NewGuid());

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCode.LabelNotFound);
        await _labels.DidNotReceive().AssignLabelsAsync(
            Arg.Any<Guid>(),
            Arg.Any<IEnumerable<Guid>>(),
            Arg.Any<Guid>());
    }

    [Fact]
    public async Task AssignLabelsToUserAsync_WithDuplicateValidIds_AssignsAndMapsOnce()
    {
        var adminId = Guid.NewGuid();
        var user = new User { Id = Guid.NewGuid(), Email = "voter@example.com" };
        var label = new Label { Id = Guid.NewGuid(), Name = "Siemens", Category = "Employer" };
        var assignment = CreateAssignment(user, label, adminId);
        var requestedIds = new[] { label.Id, label.Id };
        _users.GetByIdAsync(user.Id).Returns(user);
        _labels.GetByIdsAsync(Arg.Any<IEnumerable<Guid>>()).Returns([label]);
        _labels.AssignLabelsAsync(user.Id, Arg.Any<IEnumerable<Guid>>(), adminId)
            .Returns([assignment]);

        var result = await _service.AssignLabelsToUserAsync(
            user.Id,
            new AssignLabelsRequest { LabelIds = [.. requestedIds] },
            adminId);

        result.Success.Should().BeTrue();
        result.Data.Should().ContainSingle().Which.LabelId.Should().Be(label.Id);
        await _labels.Received(1).AssignLabelsAsync(
            user.Id,
            Arg.Is<IEnumerable<Guid>>(ids => ids != null && ids.SequenceEqual(requestedIds)),
            adminId);
    }

    [Fact]
    public async Task RemoveLabelFromUserAsync_WhenAssignmentDoesNotExist_ReturnsNotFoundWithoutSaving()
    {
        var userId = Guid.NewGuid();
        var labelId = Guid.NewGuid();
        _labels.RemoveUserLabelAsync(userId, labelId).Returns(false);

        var result = await _service.RemoveLabelFromUserAsync(userId, labelId);

        result.Success.Should().BeFalse();
        result.IsNotFound.Should().BeTrue();
        result.ErrorCode.Should().Be(ErrorCode.ResourceNotFound);
        await _labels.DidNotReceive().SaveChangesAsync();
    }

    [Fact]
    public async Task RemoveLabelFromUserAsync_WhenAssignmentExists_SavesChanges()
    {
        var userId = Guid.NewGuid();
        var labelId = Guid.NewGuid();
        _labels.RemoveUserLabelAsync(userId, labelId).Returns(true);

        var result = await _service.RemoveLabelFromUserAsync(userId, labelId);

        result.Success.Should().BeTrue();
        result.Data.Should().BeTrue();
        await _labels.Received(1).SaveChangesAsync();
    }

    [Fact]
    public async Task GetUsersWithLabelAsync_WhenLabelDoesNotExist_ReturnsNotFound()
    {
        var labelId = Guid.NewGuid();
        _labels.GetByIdAsync(labelId).Returns((Label?)null);

        var result = await _service.GetUsersWithLabelAsync(labelId);

        result.Success.Should().BeFalse();
        result.IsNotFound.Should().BeTrue();
        result.ErrorCode.Should().Be(ErrorCode.LabelNotFound);
        await _labels.DidNotReceive().GetUsersWithLabelAsync(Arg.Any<Guid>());
    }

    [Fact]
    public async Task GetUsersWithLabelAsync_WhenLabelExists_MapsUsers()
    {
        var user = new User { Id = Guid.NewGuid(), Email = "voter@example.com" };
        var assignment = CreateAssignment(user);
        _labels.GetByIdAsync(assignment.LabelId).Returns(assignment.Label);
        _labels.GetUsersWithLabelAsync(assignment.LabelId).Returns([assignment]);

        var result = await _service.GetUsersWithLabelAsync(assignment.LabelId);

        result.Success.Should().BeTrue();
        result.Data.Should().ContainSingle().Which.Should().BeEquivalentTo(new UserWithLabelDto
        {
            UserId = user.Id,
            Email = user.Email,
            AssignedAt = assignment.AssignedAt
        });
    }

    [Fact]
    public async Task GetMyLabelsAsync_ReturnsLabelsWithoutLookingUpAuthenticatedUser()
    {
        var user = new User { Id = Guid.NewGuid(), Email = "voter@example.com" };
        var assignment = CreateAssignment(user);
        _labels.GetUserLabelsAsync(user.Id).Returns([assignment]);

        var result = await _service.GetMyLabelsAsync(user.Id);

        result.Success.Should().BeTrue();
        result.Data.Should().ContainSingle().Which.LabelId.Should().Be(assignment.LabelId);
        await _users.DidNotReceive().GetByIdAsync(Arg.Any<Guid>());
    }

    private static UserLabel CreateAssignment(
        User user,
        Label? label = null,
        Guid? assignedBy = null)
    {
        label ??= new Label
        {
            Id = Guid.NewGuid(),
            Name = "Siemens",
            Category = "Employer"
        };

        return new UserLabel
        {
            UserId = user.Id,
            User = user,
            LabelId = label.Id,
            Label = label,
            AssignedBy = assignedBy ?? Guid.NewGuid(),
            AssignedAt = DateTime.UtcNow.AddMinutes(-10)
        };
    }
}
