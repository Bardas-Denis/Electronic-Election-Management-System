using Electronic_Election_Management_System.Constants;
using Electronic_Election_Management_System.Data.Repositories;
using Electronic_Election_Management_System.DTOs;
using Electronic_Election_Management_System.Models;
using Electronic_Election_Management_System.Services;
using FluentAssertions;
using NSubstitute;

namespace Electronic_Election_Management_System.Tests.Services;

public class ElectionServiceTests
{
    private readonly IElectionRepository _elections = Substitute.For<IElectionRepository>();
    private readonly IAuditLogRepository _auditLogs = Substitute.For<IAuditLogRepository>();
    private readonly IVoteRepository _votes = Substitute.For<IVoteRepository>();
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IElectionInvitationRepository _invitations =
        Substitute.For<IElectionInvitationRepository>();
    private readonly ILabelRepository _labels = Substitute.For<ILabelRepository>();
    private readonly ElectionService _service;
    private readonly Guid _creatorId = Guid.NewGuid();

    public ElectionServiceTests()
    {
        _users.GetByEmailsAsync(Arg.Any<IEnumerable<string>>()).Returns([]);
        _service = new ElectionService(
            _elections,
            _auditLogs,
            _votes,
            _users,
            _invitations,
            _labels);
    }

    [Fact]
    public async Task CreateAsync_WithUnknownType_FailsBeforePersistence()
    {
        var request = ValidCreateRequest();
        request.Type = "Unknown";

        var result = await _service.CreateAsync(request, _creatorId);

        result.ErrorCode.Should().Be(ErrorCode.InvalidElectionType);
        await _elections.DidNotReceive().AddAsync(Arg.Any<Election>());
    }

    [Fact]
    public async Task CreateAsync_WithTooFewOptions_FailsBeforePersistence()
    {
        var request = ValidCreateRequest();
        request.Options = [new CreateOptionDto { Label = "Only one" }];

        var result = await _service.CreateAsync(request, _creatorId);

        result.ErrorCode.Should().Be(ErrorCode.TooFewOptions);
        await _elections.DidNotReceive().AddAsync(Arg.Any<Election>());
    }

    [Fact]
    public async Task CreateAsync_WithInvalidDateRange_FailsBeforePersistence()
    {
        var request = ValidCreateRequest();
        request.EndsAt = request.StartsAt;

        var result = await _service.CreateAsync(request, _creatorId);

        result.ErrorCode.Should().Be(ErrorCode.InvalidDateRange);
        await _elections.DidNotReceive().AddAsync(Arg.Any<Election>());
    }

    [Fact]
    public async Task CreateAsync_WithInvitationsOnPublicElection_IsRejected()
    {
        var request = ValidCreateRequest();
        request.InvitedEmails = ["voter@example.com"];

        var result = await _service.CreateAsync(request, _creatorId);

        result.ErrorCode.Should().Be(ErrorCode.InvitationsRequireClosedElection);
        await _elections.DidNotReceive().AddAsync(Arg.Any<Election>());
    }

    [Fact]
    public async Task CreateAsync_WithLabelAudienceOnPublicElection_IsRejected()
    {
        var request = ValidCreateRequest();
        request.InvitedLabelIds = [Guid.NewGuid()];

        var result = await _service.CreateAsync(request, _creatorId);

        result.ErrorCode.Should().Be(ErrorCode.InvitationsRequireClosedElection);
        await _labels.DidNotReceive().GetByIdsAsync(Arg.Any<IEnumerable<Guid>>());
        await _elections.DidNotReceive().AddAsync(Arg.Any<Election>());
    }

    [Fact]
    public async Task CreateAsync_WithMissingAudienceLabel_IsRejected()
    {
        var request = ValidCreateRequest();
        request.IsClosed = true;
        request.InvitedLabelIds = [Guid.NewGuid()];
        _labels.GetByIdsAsync(Arg.Any<IEnumerable<Guid>>()).Returns([]);

        var result = await _service.CreateAsync(request, _creatorId);

        result.ErrorCode.Should().Be(ErrorCode.LabelNotFound);
        await _elections.DidNotReceive().AddAsync(Arg.Any<Election>());
    }

    [Fact]
    public async Task CreateAsync_WithOverlappingLabels_InvitesEachUserOnceAndExcludesCreator()
    {
        var firstLabel = new Label { Name = "Engineering" };
        var secondLabel = new Label { Name = "Bucharest" };
        var firstUser = new User { Email = "first@example.com" };
        var secondUser = new User { Email = "second@example.com" };
        var request = ValidCreateRequest();
        request.IsClosed = true;
        request.InvitedLabelIds = [firstLabel.Id, secondLabel.Id];

        _labels.GetByIdsAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns([firstLabel, secondLabel]);
        _labels.GetUsersWithLabelAsync(firstLabel.Id).Returns(
        [
            CreateUserLabel(firstUser, firstLabel),
            CreateUserLabel(secondUser, firstLabel)
        ]);
        _labels.GetUsersWithLabelAsync(secondLabel.Id).Returns(
        [
            CreateUserLabel(secondUser, secondLabel),
            CreateUserLabel(new User { Id = _creatorId, Email = "creator@example.com" }, secondLabel)
        ]);
        _users.GetByIdsAsync(Arg.Any<IEnumerable<Guid>>()).Returns([firstUser, secondUser]);

        Election? persisted = null;
        _elections.AddAsync(Arg.Do<Election>(election => persisted = election))
            .Returns(Task.CompletedTask);

        var result = await _service.CreateAsync(request, _creatorId);

        result.Success.Should().BeTrue();
        persisted.Should().NotBeNull();
        persisted!.Invitations.Select(invitation => invitation.UserId)
            .Should().BeEquivalentTo([firstUser.Id, secondUser.Id]);
        persisted.Invitations.Should().OnlyHaveUniqueItems(invitation => invitation.Email);
    }

    [Fact]
    public async Task GetInvitationLabelsAsync_ReturnsCountsWithoutCurrentUser()
    {
        var label = new Label { Name = "Engineering", Category = "Department" };
        var otherUser = new User { Email = "other@example.com" };
        _labels.GetAllAsync().Returns([label]);
        _labels.GetUsersWithLabelAsync(label.Id).Returns(
        [
            CreateUserLabel(otherUser, label),
            CreateUserLabel(new User { Id = _creatorId, Email = "creator@example.com" }, label)
        ]);

        var result = await _service.GetInvitationLabelsAsync(_creatorId);

        result.Should().ContainSingle();
        result[0].Id.Should().Be(label.Id);
        result[0].UserCount.Should().Be(1);
    }

    [Fact]
    public async Task CreateAsync_WithValidRequest_PersistsElectionAndAuditLog()
    {
        Election? persisted = null;
        _elections.AddAsync(Arg.Do<Election>(election => persisted = election))
            .Returns(Task.CompletedTask);

        var request = ValidCreateRequest();
        request.Title = "  Board election  ";

        var result = await _service.CreateAsync(request, _creatorId);

        result.Success.Should().BeTrue();
        persisted.Should().NotBeNull();
        var savedElection = persisted!;
        savedElection.CreatedByUserId.Should().Be(_creatorId);
        savedElection.Title.Should().Be("Board election");
        savedElection.Type.Should().Be(ElectionType.Comercial);
        savedElection.Questions.Should().ContainSingle();
        savedElection.Questions.Single().Options.Should().HaveCount(2);
        await _auditLogs.Received(1).AddAsync(Arg.Is<AuditLog>(log =>
            log != null &&
            log.UserId == _creatorId &&
            log.ElectionId == savedElection.Id &&
            log.Action == AuditAction.ElectionCreated.ToDbValue()));
        await _elections.Received(1).SaveChangesAsync();
    }

    [Fact]
    public async Task UpdateAsync_WhenUserIsNotCreator_ReturnsAuthorizationFailure()
    {
        var election = OpenElection();
        _elections.GetByIdWithOptionsAsync(election.Id).Returns(election);

        var result = await _service.UpdateAsync(
            election.Id,
            ValidUpdateRequest(),
            Guid.NewGuid());

        result.ErrorCode.Should().Be(ErrorCode.NotAuthorizedToEdit);
        _elections.DidNotReceive().RemoveOptions(Arg.Any<IEnumerable<Option>>());
        await _elections.DidNotReceive().SaveChangesAsync();
    }

    [Fact]
    public async Task UpdateAsync_WhenElectionHasVotes_DoesNotReplaceOptions()
    {
        var election = OpenElection();
        _elections.GetByIdWithOptionsAsync(election.Id).Returns(election);
        _votes.HasAnyVotesInElectionAsync(election.Id).Returns(true);

        var result = await _service.UpdateAsync(
            election.Id,
            ValidUpdateRequest(),
            election.CreatedByUserId);

        result.ErrorCode.Should().Be(ErrorCode.ElectionHasVotes);
        _elections.DidNotReceive().RemoveOptions(Arg.Any<IEnumerable<Option>>());
        await _elections.DidNotReceive().SaveChangesAsync();
    }

    [Fact]
    public async Task DeleteAsync_WhenCreatorDeletesElection_RemovesAndAuditsIt()
    {
        var election = OpenElection();
        _elections.GetByIdAsync(election.Id).Returns(election);

        var result = await _service.DeleteAsync(election.Id, election.CreatedByUserId);

        result.Success.Should().BeTrue();
        _elections.Received(1).Remove(election);
        await _auditLogs.Received(1).AddAsync(Arg.Is<AuditLog>(log =>
            log != null &&
            log.UserId == election.CreatedByUserId &&
            log.ElectionId == null &&
            log.Action == $"{AuditAction.ElectionDeleted.ToDbValue()}:{election.Title}"));
        await _elections.Received(1).SaveChangesAsync();
    }

    private static CreateElectionRequest ValidCreateRequest() => new()
    {
        Title = "Board election",
        Description = "Choose a representative",
        Question = "Who should represent the board?",
        Type = nameof(ElectionType.Comercial),
        IsAnonymous = true,
        StartsAt = DateTime.UtcNow.AddMinutes(-5),
        EndsAt = DateTime.UtcNow.AddHours(1),
        Options =
        [
            new CreateOptionDto { Label = "Alice" },
            new CreateOptionDto { Label = "Bob" }
        ]
    };

    private static UpdateElectionRequest ValidUpdateRequest()
    {
        var create = ValidCreateRequest();
        return new UpdateElectionRequest
        {
            Title = create.Title,
            Description = create.Description,
            Question = create.Question,
            Type = create.Type,
            IsAnonymous = create.IsAnonymous,
            IsClosed = create.IsClosed,
            StartsAt = create.StartsAt,
            EndsAt = create.EndsAt,
            Options = create.Options,
            Questions = create.Questions
        };
    }

    private Election OpenElection() => new()
    {
        CreatedByUserId = _creatorId,
        Title = "Existing election",
        Question = "Choose one",
        Type = ElectionType.Comercial,
        IsAnonymous = true,
        StartsAt = DateTime.UtcNow.AddMinutes(-5),
        EndsAt = DateTime.UtcNow.AddHours(1),
        Options =
        [
            new Option { Label = "Alice" },
            new Option { Label = "Bob" }
        ]
    };

    private static UserLabel CreateUserLabel(User user, Label label) => new()
    {
        UserId = user.Id,
        User = user,
        LabelId = label.Id,
        Label = label
    };
}
