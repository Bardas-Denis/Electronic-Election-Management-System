using Electronic_Election_Management_System.Constants;
using Electronic_Election_Management_System.Data.Repositories;
using Electronic_Election_Management_System.Models;
using Electronic_Election_Management_System.Plugins;
using Electronic_Election_Management_System.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Electronic_Election_Management_System.Tests.Services;

public class ResultsServiceTests
{
    private readonly IElectionRepository _elections = Substitute.For<IElectionRepository>();
    private readonly IVoteRepository _votes = Substitute.For<IVoteRepository>();
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    // No plugin is registered by default, so every scheme here takes the built-in path.
    private readonly IScoringPluginRegistry _plugins = Substitute.For<IScoringPluginRegistry>();
    private readonly ResultsService _service;

    public ResultsServiceTests()
    {
        _service = new ResultsService(
            _elections, _votes, _users, _plugins, Substitute.For<ILogger<ResultsService>>());
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

    // A non-anonymous election owned by `creatorId`, holding one free-text question.
    private (Election Election, ElectionQuestion Question) ElectionWithFreeTextQuestion(
        Guid creatorId, bool anonymous = false)
    {
        var election = new Election { Title = "Retro", IsAnonymous = anonymous, CreatedByUserId = creatorId };
        var question = new ElectionQuestion
        {
            ElectionId = election.Id,
            Text = "What would you change?",
            QuestionType = QuestionType.FreeText
        };
        election.Questions.Add(question);
        _elections.GetByIdWithOptionsAsync(election.Id).Returns(election);
        return (election, question);
    }

    private static Vote TextAnswerBy(User voter, string text, string? declaredName = null) => new()
    {
        AnswerText = text,
        UserId = voter.Id,
        User = voter,
        VoterDeclaration = declaredName is null ? null : new VoterDeclaration { FullName = declaredName }
    };

    [Fact]
    public async Task GetTextAnswerAuthorsAsync_WhenElectionIsAnonymous_RefusesEvenForAnAdmin()
    {
        var adminId = Guid.NewGuid();
        var (election, question) = ElectionWithFreeTextQuestion(adminId, anonymous: true);
        GivenUser(adminId, UserRole.Admin);

        var result = await _service.GetTextAnswerAuthorsAsync(election.Id, question.Id, adminId);

        result.ErrorCode.Should().Be(ErrorCode.VotersHiddenForAnonymousElection);
        await _votes.DidNotReceive().GetIdentifiedTextAnswersForQuestionAsync(Arg.Any<Guid>());
    }

    [Fact]
    public async Task GetTextAnswerAuthorsAsync_WhenRequesterIsNeitherAdminNorCreator_IsRefused()
    {
        var (election, question) = ElectionWithFreeTextQuestion(Guid.NewGuid());
        var outsiderId = Guid.NewGuid();
        // An ElectionManager, but of somebody else's election.
        GivenUser(outsiderId, UserRole.ElectionManager);

        var result = await _service.GetTextAnswerAuthorsAsync(election.Id, question.Id, outsiderId);

        result.ErrorCode.Should().Be(ErrorCode.NotAuthorizedToViewVoters);
        await _votes.DidNotReceive().GetIdentifiedTextAnswersForQuestionAsync(Arg.Any<Guid>());
    }

    [Fact]
    public async Task GetTextAnswerAuthorsAsync_WhenQuestionBelongsToAnotherElection_IsNotFound()
    {
        var adminId = Guid.NewGuid();
        var (election, _) = ElectionWithFreeTextQuestion(adminId);
        GivenUser(adminId, UserRole.Admin);

        var result = await _service.GetTextAnswerAuthorsAsync(election.Id, Guid.NewGuid(), adminId);

        result.IsNotFound.Should().BeTrue();
        await _votes.DidNotReceive().GetIdentifiedTextAnswersForQuestionAsync(Arg.Any<Guid>());
    }

    [Fact]
    public async Task GetTextAnswerAuthorsAsync_KeepsIdenticalAnswersApartWithTheirOwnAuthors()
    {
        var creatorId = Guid.NewGuid();
        var (election, question) = ElectionWithFreeTextQuestion(creatorId);
        GivenUser(creatorId, UserRole.ElectionManager);
        var ana = new User { Id = Guid.NewGuid(), Email = "ana@test.com" };
        var bogdan = new User { Id = Guid.NewGuid(), Email = "bogdan@test.com" };
        // The whole reason text and author travel together: as bare strings these two
        // answers are indistinguishable, so nothing could pair them with the right person.
        _votes.GetIdentifiedTextAnswersForQuestionAsync(question.Id).Returns(new List<Vote>
        {
            TextAnswerBy(ana, "Nothing"),
            TextAnswerBy(bogdan, "Nothing")
        });
        _users.GetUserDetailsForUsersAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns(new List<UserDetails> { new() { UserId = ana.Id, FullName = "Ana Pop" } });

        var result = await _service.GetTextAnswerAuthorsAsync(election.Id, question.Id, creatorId);

        result.Success.Should().BeTrue();
        result.Data.Should().HaveCount(2);
        result.Data!.Should().OnlyContain(a => a.AnswerText == "Nothing");
        result.Data.Should().ContainSingle(a => a.Email == "ana@test.com").Which.FullName.Should().Be("Ana Pop");
        // No profile and no declaration - the caller falls back to the email.
        result.Data.Should().ContainSingle(a => a.Email == "bogdan@test.com").Which.FullName.Should().BeNull();
    }

    [Fact]
    public async Task GetTextAnswerAuthorsAsync_PrefersTheAccountNameOverTheOneDeclaredForTheVote()
    {
        var adminId = Guid.NewGuid();
        var (election, question) = ElectionWithFreeTextQuestion(adminId);
        GivenUser(adminId, UserRole.Admin);
        var voter = new User { Id = Guid.NewGuid(), Email = "voter@test.com" };
        _votes.GetIdentifiedTextAnswersForQuestionAsync(question.Id).Returns(new List<Vote>
        {
            TextAnswerBy(voter, "More tests", declaredName: "Ana Maria Pop")
        });
        _users.GetUserDetailsForUsersAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns(new List<UserDetails> { new() { UserId = voter.Id, FullName = "Ana Pop" } });

        var result = await _service.GetTextAnswerAuthorsAsync(election.Id, question.Id, adminId);

        result.Data!.Single().FullName.Should().Be("Ana Pop");
    }

    [Fact]
    public async Task GetTextAnswerAuthorsAsync_FallsBackToTheDeclaredNameWhenTheAccountHasNone()
    {
        var adminId = Guid.NewGuid();
        var (election, question) = ElectionWithFreeTextQuestion(adminId);
        GivenUser(adminId, UserRole.Admin);
        var voter = new User { Id = Guid.NewGuid(), Email = "voter@test.com" };
        _votes.GetIdentifiedTextAnswersForQuestionAsync(question.Id).Returns(new List<Vote>
        {
            TextAnswerBy(voter, "More tests", declaredName: "Ana Maria Pop")
        });
        _users.GetUserDetailsForUsersAsync(Arg.Any<IEnumerable<Guid>>()).Returns(new List<UserDetails>());

        var result = await _service.GetTextAnswerAuthorsAsync(election.Id, question.Id, adminId);

        result.Data!.Single().FullName.Should().Be("Ana Maria Pop");
    }

    [Fact]
    public async Task GetVotersAsync_WhenElectionIsAnonymous_RefusesEvenForAnAdmin()
    {
        var adminId = Guid.NewGuid();
        var election = new Election { Title = "Secret ballot", IsAnonymous = true, CreatedByUserId = adminId };
        var option = new Option { ElectionId = election.Id, Label = "Alice" };
        election.Options.Add(option);
        _elections.GetByIdWithOptionsAsync(election.Id).Returns(election);
        GivenUser(adminId, UserRole.Admin);

        var result = await _service.GetVotersAsync(election.Id, null, adminId);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCode.VotersHiddenForAnonymousElection);
        await _votes.DidNotReceive().GetIdentifiedVotesForOptionsAsync(Arg.Any<IEnumerable<Guid>>());
    }

    [Fact]
    public async Task GetVotersAsync_WhenRequesterIsNeitherAdminNorCreator_IsRefused()
    {
        var (election, option) = ElectionWithOption(Guid.NewGuid());
        var outsiderId = Guid.NewGuid();
        // An ElectionManager, but of somebody else's election.
        GivenUser(outsiderId, UserRole.ElectionManager);

        var result = await _service.GetVotersAsync(election.Id, null, outsiderId);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCode.NotAuthorizedToViewVoters);
        await _votes.DidNotReceive().GetIdentifiedVotesForOptionsAsync(Arg.Any<IEnumerable<Guid>>());
    }

    [Fact]
    public async Task GetVotersAsync_WhenRequesterCreatedTheElection_ReturnsTheVoters()
    {
        var creatorId = Guid.NewGuid();
        var (election, option) = ElectionWithOption(creatorId);
        GivenUser(creatorId, UserRole.ElectionManager);
        var voter = new User { Id = Guid.NewGuid(), Email = "voter@test.com" };
        _votes.GetIdentifiedVotesForOptionsAsync(Arg.Any<IEnumerable<Guid>>()).Returns(new List<Vote>
        {
            new() { OptionId = option.Id, UserId = voter.Id, User = voter }
        });
        _users.GetUserDetailsForUsersAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns(new List<UserDetails> { new() { UserId = voter.Id, FullName = "Ana Pop" } });

        var result = await _service.GetVotersAsync(election.Id, null, creatorId);

        result.Success.Should().BeTrue();
        var group = result.Data.Should().ContainSingle().Which;
        group.Label.Should().Be("Alice");
        group.Voters.Should().ContainSingle().Which.Email.Should().Be("voter@test.com");
        group.Voters[0].FullName.Should().Be("Ana Pop");
    }

    [Fact]
    public async Task GetVotersAsync_PrefersTheAccountNameOverTheOneDeclaredForTheVote()
    {
        var creatorId = Guid.NewGuid();
        var (election, option) = ElectionWithOption(creatorId);
        GivenUser(creatorId, UserRole.Admin);
        var voter = new User { Id = Guid.NewGuid(), Email = "voter@test.com" };
        _votes.GetIdentifiedVotesForOptionsAsync(Arg.Any<IEnumerable<Guid>>()).Returns(new List<Vote>
        {
            new()
            {
                OptionId = option.Id,
                UserId = voter.Id,
                User = voter,
                VoterDeclaration = new VoterDeclaration { FullName = "Ana Maria Pop" }
            }
        });
        _users.GetUserDetailsForUsersAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns(new List<UserDetails> { new() { UserId = voter.Id, FullName = "Ana Pop" } });

        var result = await _service.GetVotersAsync(election.Id, null, creatorId);

        result.Data!.Single().Voters.Single().FullName.Should().Be("Ana Pop");
    }

    [Fact]
    public async Task GetVotersAsync_WhenNeitherNameExists_LeavesFullNameNull()
    {
        var creatorId = Guid.NewGuid();
        var (election, option) = ElectionWithOption(creatorId);
        GivenUser(creatorId, UserRole.Admin);
        var voter = new User { Id = Guid.NewGuid(), Email = "voter@test.com" };
        _votes.GetIdentifiedVotesForOptionsAsync(Arg.Any<IEnumerable<Guid>>()).Returns(new List<Vote>
        {
            new() { OptionId = option.Id, UserId = voter.Id, User = voter }
        });
        _users.GetUserDetailsForUsersAsync(Arg.Any<IEnumerable<Guid>>()).Returns(new List<UserDetails>());

        var result = await _service.GetVotersAsync(election.Id, null, creatorId);

        result.Data!.Single().Voters.Single().FullName.Should().BeNull();
    }

    [Fact]
    public async Task GetVotersAsync_WhenTheQuestionIsNotPartOfTheElection_IsNotFound()
    {
        var adminId = Guid.NewGuid();
        var (election, _) = ElectionWithOption(adminId);
        GivenUser(adminId, UserRole.Admin);

        var result = await _service.GetVotersAsync(election.Id, Guid.NewGuid(), adminId);

        result.IsNotFound.Should().BeTrue();
        await _votes.DidNotReceive().GetIdentifiedVotesForOptionsAsync(Arg.Any<IEnumerable<Guid>>());
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
