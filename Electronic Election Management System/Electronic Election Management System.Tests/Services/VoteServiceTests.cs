using Electronic_Election_Management_System.Constants;
using Electronic_Election_Management_System.Data.Repositories;
using Electronic_Election_Management_System.DTOs;
using Electronic_Election_Management_System.Hubs;
using Electronic_Election_Management_System.Models;
using Electronic_Election_Management_System.Services;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Electronic_Election_Management_System.Tests.Services;

public class VoteServiceTests
{
    private readonly IElectionRepository _elections = Substitute.For<IElectionRepository>();
    private readonly IVoteRepository _votes = Substitute.For<IVoteRepository>();
    private readonly ICnpService _cnp = Substitute.For<ICnpService>();
    private readonly IResultsService _results = Substitute.For<IResultsService>();
    private readonly IHubContext<ResultsHub> _hub = Substitute.For<IHubContext<ResultsHub>>();
    private readonly ILogger<VoteService> _logger = Substitute.For<ILogger<VoteService>>();
    private readonly VoteService _service;
    private readonly Guid _userId = Guid.NewGuid();

    public VoteServiceTests()
    {
        _service = new VoteService(_elections, _votes, _cnp, _results, _hub, _logger);
    }

    [Fact]
    public async Task CastVoteAsync_WhenElectionIsMissing_ReturnsNotFound()
    {
        var request = new CastVoteRequest
        {
            ElectionId = Guid.NewGuid(),
            OptionId = Guid.NewGuid()
        };
        _elections.GetAccessibleByIdWithOptionsAsync(request.ElectionId, _userId)
            .Returns((Election?)null);

        var result = await _service.CastVoteAsync(request, _userId);

        result.IsNotFound.Should().BeTrue();
        result.ErrorCode.Should().Be(ErrorCode.ResourceNotFound);
        await _votes.DidNotReceive().AddVoteAsync(Arg.Any<Vote>());
    }

    [Fact]
    public async Task CastVoteAsync_WhenElectionIsClosed_ReturnsElectionNotOpen()
    {
        var election = ElectionWithOneQuestion();
        election.EndsAt = DateTime.UtcNow.AddMinutes(-1);
        _elections.GetAccessibleByIdWithOptionsAsync(election.Id, _userId).Returns(election);

        var result = await _service.CastVoteAsync(RequestFor(election.Options.First()), _userId);

        result.ErrorCode.Should().Be(ErrorCode.ElectionNotOpen);
        await _votes.DidNotReceive().AddVoteAsync(Arg.Any<Vote>());
    }

    [Fact]
    public async Task CastVoteAsync_WithOptionFromAnotherElection_ReturnsInvalidOption()
    {
        var election = ElectionWithOneQuestion();
        _elections.GetAccessibleByIdWithOptionsAsync(election.Id, _userId).Returns(election);

        var result = await _service.CastVoteAsync(new CastVoteRequest
        {
            ElectionId = election.Id,
            OptionId = Guid.NewGuid()
        }, _userId);

        result.ErrorCode.Should().Be(ErrorCode.InvalidOption);
        await _votes.DidNotReceive().AddVoteAsync(Arg.Any<Vote>());
    }

    [Fact]
    public async Task CastVoteAsync_WithIncompleteMultiQuestionSelection_ReturnsInvalidOption()
    {
        var election = ElectionWithTwoQuestions();
        _elections.GetAccessibleByIdWithOptionsAsync(election.Id, _userId).Returns(election);

        var result = await _service.CastVoteAsync(RequestFor(election.Options.First()), _userId);

        result.ErrorCode.Should().Be(ErrorCode.InvalidOption);
        await _votes.DidNotReceive().AddVoteAsync(Arg.Any<Vote>());
    }

    [Fact]
    public async Task CastVoteAsync_ForAnonymousElection_NeverLinksVoteToUser()
    {
        var election = ElectionWithOneQuestion();
        var selectedOption = election.Options.First();
        _elections.GetAccessibleByIdWithOptionsAsync(election.Id, _userId).Returns(election);
        _votes.GetVoteTokenAsync(_userId, election.Id).Returns((VoteToken?)null);

        VoteToken? issuedToken = null;
        Vote? persistedVote = null;
        _votes.AddVoteTokenAsync(Arg.Do<VoteToken>(token => issuedToken = token))
            .Returns(Task.CompletedTask);
        _votes.AddVoteAsync(Arg.Do<Vote>(vote => persistedVote = vote))
            .Returns(Task.CompletedTask);

        var result = await _service.CastVoteAsync(RequestFor(selectedOption), _userId);

        result.Success.Should().BeTrue();
        issuedToken.Should().NotBeNull();
        issuedToken!.IsUsed.Should().BeTrue();
        issuedToken.UserId.Should().Be(_userId);
        persistedVote.Should().NotBeNull();
        persistedVote!.OptionId.Should().Be(selectedOption.Id);
        persistedVote.VoteTokenId.Should().Be(issuedToken.Id);
        persistedVote.UserId.Should().BeNull();
        await _votes.DidNotReceive().AddVoterDeclarationAsync(Arg.Any<VoterDeclaration>());
        await _votes.Received(1).SaveChangesAsync();
    }

    [Fact]
    public async Task CastVoteAsync_WhenAnonymousTokenWasAlreadyUsed_ReturnsAlreadyVoted()
    {
        var election = ElectionWithOneQuestion();
        _elections.GetAccessibleByIdWithOptionsAsync(election.Id, _userId).Returns(election);
        _votes.GetVoteTokenAsync(_userId, election.Id).Returns(new VoteToken
        {
            UserId = _userId,
            ElectionId = election.Id,
            IsUsed = true
        });

        var result = await _service.CastVoteAsync(RequestFor(election.Options.First()), _userId);

        result.ErrorCode.Should().Be(ErrorCode.AlreadyVoted);
        await _votes.DidNotReceive().AddVoteAsync(Arg.Any<Vote>());
        await _votes.DidNotReceive().SaveChangesAsync();
    }

    [Fact]
    public async Task CastVoteAsync_ForIdentifiedElectionWithoutDeclaration_IsRejected()
    {
        var election = ElectionWithOneQuestion();
        election.IsAnonymous = false;
        _elections.GetAccessibleByIdWithOptionsAsync(election.Id, _userId).Returns(election);

        var result = await _service.CastVoteAsync(RequestFor(election.Options.First()), _userId);

        result.ErrorCode.Should().Be(ErrorCode.DeclarationRequired);
        await _votes.DidNotReceive().AddVoteAsync(Arg.Any<Vote>());
    }

    [Fact]
    public async Task UpdateVoteAsync_AfterAllowedChangeWasUsed_ReturnsVoteChangeLimit()
    {
        var election = ElectionWithOneQuestion();
        var token = new VoteToken
        {
            UserId = _userId,
            ElectionId = election.Id,
            IsUsed = true,
            Votes = [new Vote { OptionId = election.Options.First().Id }]
        };
        _elections.GetAccessibleByIdWithOptionsAsync(election.Id, _userId).Returns(election);
        _votes.GetVoteTokenWithVotesAsync(_userId, election.Id).Returns(token);
        _votes.GetChangeCountAsync(_userId, election.Id).Returns(1);

        var result = await _service.UpdateVoteAsync(RequestFor(election.Options.Last()), _userId);

        result.ErrorCode.Should().Be(ErrorCode.VoteChangeLimit);
        _votes.DidNotReceive().RemoveVotes(Arg.Any<IEnumerable<Vote>>());
        await _votes.DidNotReceive().SaveChangesAsync();
    }

    [Fact]
    public async Task DeleteVoteAsync_ForAnonymousVote_FreesTokenAndConsumesChange()
    {
        var election = ElectionWithOneQuestion();
        var existingVotes = new List<Vote>
        {
            new() { OptionId = election.Options.First().Id }
        };
        var token = new VoteToken
        {
            UserId = _userId,
            ElectionId = election.Id,
            IsUsed = true,
            Votes = existingVotes
        };
        _elections.GetAccessibleByIdWithOptionsAsync(election.Id, _userId).Returns(election);
        _votes.GetVoteTokenWithVotesAsync(_userId, election.Id).Returns(token);
        _votes.GetChangeCountAsync(_userId, election.Id).Returns(0);

        var result = await _service.DeleteVoteAsync(election.Id, _userId);

        result.Success.Should().BeTrue();
        token.IsUsed.Should().BeFalse();
        _votes.Received(1).RemoveVotes(existingVotes);
        await _votes.Received(1).IncrementChangeCountAsync(_userId, election.Id);
        await _votes.Received(1).SaveChangesAsync();
    }

    [Fact]
    public async Task CastVoteAsync_WhenSignalRBroadcastFails_StillReturnsSuccess()
    {
        var election = ElectionWithOneQuestion();
        _elections.GetAccessibleByIdWithOptionsAsync(election.Id, _userId).Returns(election);
        _results.GetResultsAsync(election.Id).Returns(new ElectionResultsDto
        {
            ElectionId = election.Id,
            Title = election.Title
        });

        var clients = Substitute.For<IHubClients>();
        var proxy = Substitute.For<IClientProxy>();
        _hub.Clients.Returns(clients);
        clients.Group(election.Id.ToString()).Returns(proxy);
        proxy.SendCoreAsync(
                Arg.Any<string>(),
                Arg.Any<object?[]>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("Hub unavailable")));

        var result = await _service.CastVoteAsync(RequestFor(election.Options.First()), _userId);

        result.Success.Should().BeTrue();
        await _votes.Received(1).SaveChangesAsync();
    }

    private static CastVoteRequest RequestFor(Option option) => new()
    {
        ElectionId = option.ElectionId,
        OptionId = option.Id
    };

    private static Election ElectionWithOneQuestion()
    {
        var election = NewOpenElection();
        AddQuestion(election, "Choose a representative", "Alice", "Bob");
        return election;
    }

    private static Election ElectionWithTwoQuestions()
    {
        var election = NewOpenElection();
        AddQuestion(election, "Choose a representative", "Alice", "Bob");
        AddQuestion(election, "Choose a location", "North", "South");
        return election;
    }

    private static Election NewOpenElection() => new()
    {
        Title = "Open election",
        Type = ElectionType.Comercial,
        IsAnonymous = true,
        StartsAt = DateTime.UtcNow.AddMinutes(-5),
        EndsAt = DateTime.UtcNow.AddHours(1)
    };

    private static void AddQuestion(Election election, string text, params string[] labels)
    {
        var question = new ElectionQuestion
        {
            ElectionId = election.Id,
            Text = text,
            DisplayOrder = election.Questions.Count
        };
        foreach (var label in labels)
        {
            var option = new Option
            {
                ElectionId = election.Id,
                QuestionId = question.Id,
                Question = question,
                Label = label
            };
            question.Options.Add(option);
            election.Options.Add(option);
        }
        election.Questions.Add(question);
    }
}
