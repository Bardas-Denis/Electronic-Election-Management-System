using Electronic_Election_Management_System.Constants;
using Electronic_Election_Management_System.Data.Repositories;
using Electronic_Election_Management_System.DTOs;
using Electronic_Election_Management_System.Models;
using Electronic_Election_Management_System.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
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
    private readonly ILogger<ElectionService> _logger = Substitute.For<ILogger<ElectionService>>();
    private readonly ElectionService _service;
    private readonly Guid _creatorId = Guid.NewGuid();

    public ElectionServiceTests()
    {
        _users.GetByEmailsAsync(Arg.Any<IEnumerable<string>>()).Returns([]);
        _users.GetByIdsAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns(callInfo => callInfo.Arg<IEnumerable<Guid>>().Select(id => new User { Id = id, Email = $"user{id}@example.com" }).ToList());
        _service = new ElectionService(
            _elections,
            _auditLogs,
            _votes,
            _users,
            _invitations,
            _labels,
            _logger);
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

    // -------------------------------------------------------------------------
    // CreateAsync — closed election paths


    [Fact]
    public async Task CreateAsync_ClosedElection_WithEmailInvitees_PersistsInvitations()
    {
        Election? persisted = null;
        _elections.AddAsync(Arg.Do<Election>(e => persisted = e))
            .Returns(Task.CompletedTask);

        var request = ValidCreateRequest();
        request.IsClosed = true;
        request.InvitedEmails = ["voter@example.com"];

        var result = await _service.CreateAsync(request, _creatorId);

        result.Success.Should().BeTrue();
        persisted!.IsClosed.Should().BeTrue();
        persisted.Invitations.Should().ContainSingle(inv =>
            inv.Email == "voter@example.com");
    }

    [Fact]
    public async Task CreateAsync_ClosedElection_WithInvalidEmail_IsRejected()
    {
        var request = ValidCreateRequest();
        request.IsClosed = true;
        request.InvitedEmails = ["not-an-email"];

        var result = await _service.CreateAsync(request, _creatorId);

        result.ErrorCode.Should().Be(ErrorCode.InvalidInvitationEmail);
        await _elections.DidNotReceive().AddAsync(Arg.Any<Election>());
    }

    [Fact]
    public async Task CreateAsync_ClosedElection_WithNonExistentUserId_IsRejected()
    {
        _users.GetByIdsAsync(Arg.Any<IEnumerable<Guid>>()).Returns([]);

        var request = ValidCreateRequest();
        request.IsClosed = true;
        request.InvitedUserIds = [Guid.NewGuid()];

        var result = await _service.CreateAsync(request, _creatorId);

        result.ErrorCode.Should().Be(ErrorCode.InvitedUserNotFound);
        await _elections.DidNotReceive().AddAsync(Arg.Any<Election>());
    }

    [Fact]
    public async Task CreateAsync_ClosedElection_CreatorEmailIsExcludedFromInvitees()
    {
        var creator = new User { Id = _creatorId, Email = "creator@example.com" };
        _users.GetByIdAsync(_creatorId).Returns(creator);
        _users.GetByEmailsAsync(Arg.Any<IEnumerable<string>>()).Returns([creator]);

        Election? persisted = null;
        _elections.AddAsync(Arg.Do<Election>(e => persisted = e))
            .Returns(Task.CompletedTask);

        var request = ValidCreateRequest();
        request.IsClosed = true;
        // Invite the creator's own email — should be silently dropped
        request.InvitedEmails = [creator.Email];

        var result = await _service.CreateAsync(request, _creatorId);

        result.Success.Should().BeTrue();
        persisted!.Invitations.Should().BeEmpty();
    }

    // -------------------------------------------------------------------------
    // UpdateAsync — closed election
    // -------------------------------------------------------------------------

    [Fact]
    public async Task UpdateAsync_WhenValid_UpdatesScalarFieldsAndPersists()
    {
        var election = OpenElection();
        _elections.GetByIdWithOptionsAsync(election.Id).Returns(election);
        _votes.HasAnyVotesInElectionAsync(election.Id).Returns(false);

        var request = ValidUpdateRequest();
        request.Title = "  Updated title  ";
        request.IsClosed = false;

        var result = await _service.UpdateAsync(election.Id, request, election.CreatedByUserId);

        result.Success.Should().BeTrue();
        election.Title.Should().Be("Updated title");
        await _elections.Received(1).AddQuestionsAsync(Arg.Any<IEnumerable<ElectionQuestion>>());
        await _elections.Received(1).SaveChangesAsync();
    }

    // -------------------------------------------------------------------------
    // InviteAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task InviteAsync_OnPublicElection_IsRejected()
    {
        var election = OpenElection(); // IsClosed = false
        _elections.GetByIdAsync(election.Id).Returns(election);

        var result = await _service.InviteAsync(
            election.Id,
            new InviteToElectionRequest { UserIds = [], Emails = [] },
            _creatorId);

        result.ErrorCode.Should().Be(ErrorCode.InvitationsRequireClosedElection);
        await _invitations.DidNotReceive().AddRangeAsync(Arg.Any<IEnumerable<ElectionInvitation>>());
    }

    [Fact]
    public async Task InviteAsync_ByNonCreator_IsRejected()
    {
        var election = ClosedElection();
        _elections.GetByIdAsync(election.Id).Returns(election);

        var result = await _service.InviteAsync(
            election.Id,
            new InviteToElectionRequest { UserIds = [], Emails = [] },
            Guid.NewGuid()); // different user

        result.ErrorCode.Should().Be(ErrorCode.NotAuthorizedToManageInvitations);
        await _invitations.DidNotReceive().AddRangeAsync(Arg.Any<IEnumerable<ElectionInvitation>>());
    }

    [Fact]
    public async Task InviteAsync_WithInvalidEmail_IsRejected()
    {
        var election = ClosedElection();
        _elections.GetByIdAsync(election.Id).Returns(election);

        var result = await _service.InviteAsync(
            election.Id,
            new InviteToElectionRequest { Emails = ["bad-email"] },
            _creatorId);

        result.ErrorCode.Should().Be(ErrorCode.InvalidInvitationEmail);
        await _invitations.DidNotReceive().AddRangeAsync(Arg.Any<IEnumerable<ElectionInvitation>>());
    }

    [Fact]
    public async Task InviteAsync_WithNonExistentUserId_IsRejected()
    {
        var election = ClosedElection();
        _elections.GetByIdAsync(election.Id).Returns(election);
        _users.GetByIdsAsync(Arg.Any<IEnumerable<Guid>>()).Returns([]);

        var result = await _service.InviteAsync(
            election.Id,
            new InviteToElectionRequest { UserIds = [Guid.NewGuid()] },
            _creatorId);

        result.ErrorCode.Should().Be(ErrorCode.InvitedUserNotFound);
        await _invitations.DidNotReceive().AddRangeAsync(Arg.Any<IEnumerable<ElectionInvitation>>());
    }

    [Fact]
    public async Task InviteAsync_WithDuplicateEmail_SkipsAlreadyInvited()
    {
        var election = ClosedElection();
        _elections.GetByIdAsync(election.Id).Returns(election);
        // Simulate the email is already in the invitation list
        _invitations.GetExistingEmailsAsync(election.Id, Arg.Any<IEnumerable<string>>())
            .Returns(["voter@example.com"]);
        _invitations.GetByElectionAsync(election.Id).Returns([]);

        var result = await _service.InviteAsync(
            election.Id,
            new InviteToElectionRequest { Emails = ["voter@example.com"] },
            _creatorId);

        result.Success.Should().BeTrue();
        // No new invitations added because the email already exists
        await _invitations.DidNotReceive().AddRangeAsync(Arg.Any<IEnumerable<ElectionInvitation>>());
    }

    [Fact]
    public async Task InviteAsync_WithValidEmail_AddsInvitationAndAudits()
    {
        var election = ClosedElection();
        _elections.GetByIdAsync(election.Id).Returns(election);
        _invitations.GetExistingEmailsAsync(election.Id, Arg.Any<IEnumerable<string>>())
            .Returns([]);
        _invitations.GetByElectionAsync(election.Id).Returns([]);

        var result = await _service.InviteAsync(
            election.Id,
            new InviteToElectionRequest { Emails = ["newvoter@example.com"] },
            _creatorId);

        result.Success.Should().BeTrue();
        await _invitations.Received(1).AddRangeAsync(
            Arg.Is<IEnumerable<ElectionInvitation>>(list =>
                list.Any(inv => inv.Email == "newvoter@example.com")));
        await _auditLogs.Received(1).AddAsync(Arg.Is<AuditLog>(log =>
            log.Action == AuditAction.ElectionInvitationsAdded.ToDbValue()));
    }

    // -------------------------------------------------------------------------
    // RemoveInvitationAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task RemoveInvitationAsync_ByNonCreator_IsRejected()
    {
        var election = ClosedElection();
        _elections.GetByIdAsync(election.Id).Returns(election);

        var result = await _service.RemoveInvitationAsync(
            election.Id, Guid.NewGuid(), Guid.NewGuid());

        result.ErrorCode.Should().Be(ErrorCode.NotAuthorizedToManageInvitations);
        _invitations.DidNotReceive().Remove(Arg.Any<ElectionInvitation>());
    }

    [Fact]
    public async Task RemoveInvitationAsync_WithInvitationFromDifferentElection_IsRejected()
    {
        var election = ClosedElection();
        _elections.GetByIdAsync(election.Id).Returns(election);
        // Invitation belongs to a different election
        var invitation = new ElectionInvitation
        {
            ElectionId = Guid.NewGuid(), // ← different
            Email = "voter@example.com"
        };
        _invitations.GetByIdAsync(invitation.Id).Returns(invitation);

        var result = await _service.RemoveInvitationAsync(
            election.Id, invitation.Id, _creatorId);

        result.IsNotFound.Should().BeTrue();
        _invitations.DidNotReceive().Remove(Arg.Any<ElectionInvitation>());
    }

    [Fact]
    public async Task RemoveInvitationAsync_WithValidInvitation_RemovesAndAudits()
    {
        var election = ClosedElection();
        _elections.GetByIdAsync(election.Id).Returns(election);
        var invitation = new ElectionInvitation
        {
            ElectionId = election.Id,
            Email = "voter@example.com"
        };
        _invitations.GetByIdAsync(invitation.Id).Returns(invitation);

        var result = await _service.RemoveInvitationAsync(
            election.Id, invitation.Id, _creatorId);

        result.Success.Should().BeTrue();
        _invitations.Received(1).Remove(invitation);
        await _auditLogs.Received(1).AddAsync(Arg.Is<AuditLog>(log =>
            log.Action == AuditAction.ElectionInvitationRemoved.ToDbValue() &&
            log.ElectionId == election.Id));
        await _invitations.Received(1).SaveChangesAsync();
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
        IsClosed = false,
        StartsAt = DateTime.UtcNow.AddMinutes(-5),
        EndsAt = DateTime.UtcNow.AddHours(1),
        Options =
        [
            new Option { Label = "Alice" },
            new Option { Label = "Bob" }
        ]
    };

    private Election ClosedElection() => new()
    {
        CreatedByUserId = _creatorId,
        Title = "Closed election",
        Question = "Choose one",
        Type = ElectionType.Comercial,
        IsAnonymous = true,
        IsClosed = true,
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
