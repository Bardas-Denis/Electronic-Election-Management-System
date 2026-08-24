using Electronic_Election_Management_System.Constants;
using Electronic_Election_Management_System.Data.Repositories;
using Electronic_Election_Management_System.Models;
using Electronic_Election_Management_System.Services;
using FluentAssertions;
using NSubstitute;

namespace Electronic_Election_Management_System.Tests.Services;

public class ResultsServiceTests
{
    private readonly IElectionRepository _elections = Substitute.For<IElectionRepository>();
    private readonly IVoteRepository _votes = Substitute.For<IVoteRepository>();
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly ResultsService _service;

    public ResultsServiceTests()
    {
        _service = new ResultsService(_elections, _votes, _users);
    }

    // Builds a non-anonymous election owned by `creatorId` holding one option.
    private (Election Election, Option Option) ElectionWithOption(Guid creatorId)
    {
        var election = new Election { Title = "Named vote", IsAnonymous = false, CreatedByUserId = creatorId };
        var option = new Option { ElectionId = election.Id, Label = "Alice" };
        election.Options.Add(option);
        _elections.GetByIdWithOptionsAsync(election.Id).Returns(election);
        return (election, option);
    }

    private void GivenUser(Guid id, UserRole role)
        => _users.GetByIdAsync(id).Returns(new User { Id = id, Email = $"{id}@test.com", Role = role });

    [Fact]
    public async Task GetOptionVotersAsync_WhenElectionIsAnonymous_RefusesEvenForAnAdmin()
    {
        var adminId = Guid.NewGuid();
        var election = new Election { Title = "Secret ballot", IsAnonymous = true, CreatedByUserId = adminId };
        var option = new Option { ElectionId = election.Id, Label = "Alice" };
        election.Options.Add(option);
        _elections.GetByIdWithOptionsAsync(election.Id).Returns(election);
        GivenUser(adminId, UserRole.Admin);

        var result = await _service.GetOptionVotersAsync(election.Id, option.Id, adminId);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCode.VotersHiddenForAnonymousElection);
        await _votes.DidNotReceive().GetIdentifiedVotesForOptionAsync(Arg.Any<Guid>());
    }

    [Fact]
    public async Task GetOptionVotersAsync_WhenRequesterIsNeitherAdminNorCreator_IsRefused()
    {
        var (election, option) = ElectionWithOption(Guid.NewGuid());
        var outsiderId = Guid.NewGuid();
        // An ElectionManager, but of somebody else's election.
        GivenUser(outsiderId, UserRole.ElectionManager);

        var result = await _service.GetOptionVotersAsync(election.Id, option.Id, outsiderId);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCode.NotAuthorizedToViewVoters);
        await _votes.DidNotReceive().GetIdentifiedVotesForOptionAsync(Arg.Any<Guid>());
    }

    [Fact]
    public async Task GetOptionVotersAsync_WhenRequesterCreatedTheElection_ReturnsTheVoters()
    {
        var creatorId = Guid.NewGuid();
        var (election, option) = ElectionWithOption(creatorId);
        GivenUser(creatorId, UserRole.ElectionManager);
        var voter = new User { Id = Guid.NewGuid(), Email = "voter@test.com" };
        _votes.GetIdentifiedVotesForOptionAsync(option.Id).Returns(new List<Vote>
        {
            new() { OptionId = option.Id, UserId = voter.Id, User = voter }
        });

        var result = await _service.GetOptionVotersAsync(election.Id, option.Id, creatorId);

        result.Success.Should().BeTrue();
        result.Data.Should().ContainSingle().Which.Email.Should().Be("voter@test.com");
    }

    [Fact]
    public async Task GetOptionVotersAsync_WhenOptionBelongsToAnotherElection_IsNotFound()
    {
        var adminId = Guid.NewGuid();
        var (election, _) = ElectionWithOption(adminId);
        GivenUser(adminId, UserRole.Admin);

        var result = await _service.GetOptionVotersAsync(election.Id, Guid.NewGuid(), adminId);

        result.IsNotFound.Should().BeTrue();
        await _votes.DidNotReceive().GetIdentifiedVotesForOptionAsync(Arg.Any<Guid>());
    }

    [Fact]
    public async Task GetResultsAsync_WhenElectionDoesNotExist_ReturnsNull()
    {
        var electionId = Guid.NewGuid();
        _elections.GetByIdWithResultsAsync(electionId).Returns((Election?)null);

        var result = await _service.GetResultsAsync(electionId);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetResultsAsync_TalliesEveryQuestionAndOption()
    {
        var election = new Election { Title = "Two questions" };
        AddQuestionWithVotes(election, "President", ("Alice", 2), ("Bob", 1));
        AddQuestionWithVotes(election, "Location", ("North", 1), ("South", 2));
        _elections.GetByIdWithResultsAsync(election.Id).Returns(election);

        var result = await _service.GetResultsAsync(election.Id);

        result.Should().NotBeNull();
        result!.TotalVotes.Should().Be(3);
        result.Questions.Should().HaveCount(2);
        result.Questions.Should().OnlyContain(question => question.TotalVotes == 3);
        result.Questions[0].Results.Single(option => option.Label == "Alice")
            .VoteCount.Should().Be(2);
        result.Results.Should().BeEquivalentTo(result.Questions[0].Results);
    }

    [Fact]
    public async Task GetResultsAsync_WhenChoiceQuestionAllowsOther_AddsOtherEntryAndCountsItOnce()
    {
        var election = new Election { Title = "With other" };
        var question = new ElectionQuestion
        {
            ElectionId = election.Id,
            Text = "Pick one",
            DisplayOrder = 0,
            QuestionType = QuestionType.Choice,
            AllowOtherOption = true
        };
        var option = new Option
        {
            ElectionId = election.Id,
            QuestionId = question.Id,
            Question = question,
            Label = "Alice",
            Votes = Enumerable.Range(0, 2).Select(_ => new Vote()).ToList()
        };
        question.Options.Add(option);
        election.Options.Add(option);
        question.Votes.Add(new Vote { QuestionId = question.Id, AnswerText = "Carol" });
        election.Questions.Add(question);
        _elections.GetByIdWithResultsAsync(election.Id).Returns(election);

        var result = await _service.GetResultsAsync(election.Id);

        result.Should().NotBeNull();
        var resultQuestion = result!.Questions.Single();
        // The "Other" answer must be counted once in the total, not skipped or double-counted.
        resultQuestion.TotalVotes.Should().Be(3);
        resultQuestion.Results.Should().ContainSingle(r => r.IsOtherOption)
            .Which.VoteCount.Should().Be(1);
        resultQuestion.Results.Sum(r => r.VoteCount).Should().Be(3);
    }

    [Fact]
    public async Task GetResultsAsync_WhenUserCannotAccessElection_DoesNotLoadResults()
    {
        var electionId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        _elections.CanUserAccessAsync(electionId, userId).Returns(false);

        var result = await _service.GetResultsAsync(electionId, userId);

        result.Should().BeNull();
        await _elections.DidNotReceive().GetByIdWithResultsAsync(electionId);
    }

    private static void AddQuestionWithVotes(
        Election election,
        string text,
        params (string Label, int Votes)[] choices)
    {
        var question = new ElectionQuestion
        {
            ElectionId = election.Id,
            Text = text,
            DisplayOrder = election.Questions.Count
        };
        foreach (var choice in choices)
        {
            var option = new Option
            {
                ElectionId = election.Id,
                QuestionId = question.Id,
                Question = question,
                Label = choice.Label,
                Votes = Enumerable.Range(0, choice.Votes)
                    .Select(_ => new Vote())
                    .ToList()
            };
            question.Options.Add(option);
            election.Options.Add(option);
        }
        election.Questions.Add(question);
    }
}
