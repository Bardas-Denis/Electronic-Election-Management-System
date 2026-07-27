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
    private readonly ElectionService _service;
    private readonly Guid _creatorId = Guid.NewGuid();

    public ElectionServiceTests()
    {
        _service = new ElectionService(_elections, _auditLogs, _votes, _users, _invitations);
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
}
