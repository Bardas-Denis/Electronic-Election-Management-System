using Electronic_Election_Management_System.Data.Repositories;
using Electronic_Election_Management_System.Models;
using Electronic_Election_Management_System.Services;
using FluentAssertions;
using NSubstitute;

namespace Electronic_Election_Management_System.Tests.Services;

public class ResultsServiceTests
{
    private readonly IElectionRepository _elections = Substitute.For<IElectionRepository>();
    private readonly ResultsService _service;

    public ResultsServiceTests()
    {
        _service = new ResultsService(_elections);
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
