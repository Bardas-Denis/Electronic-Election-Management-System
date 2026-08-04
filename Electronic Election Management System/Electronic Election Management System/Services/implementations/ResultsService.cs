using Electronic_Election_Management_System.Data.Repositories;
using Electronic_Election_Management_System.DTOs;

namespace Electronic_Election_Management_System.Services
{
    public interface IResultsService
    {
        Task<ElectionResultsDto?> GetResultsAsync(Guid electionId);
        Task<ElectionResultsDto?> GetResultsAsync(Guid electionId, Guid userId);
    }

    public class ResultsService : IResultsService
    {
        private readonly IElectionRepository _elections;

        public ResultsService(IElectionRepository elections)
        {
            _elections = elections;
        }

        public async Task<ElectionResultsDto?> GetResultsAsync(Guid electionId)
        {
            var election = await _elections.GetByIdWithResultsAsync(electionId);
            if (election is null)
                return null;

            var questions = election.Questions
                .OrderBy(q => q.DisplayOrder)
                .Select(q => new QuestionResultDto
                {
                    QuestionId = q.Id,
                    Text = q.Text,
                    AllowMultipleAnswers = q.AllowMultipleAnswers,
                    Results = q.Options.Select(o => new OptionResultDto
                    {
                        OptionId = o.Id,
                        Label = o.Label,
                        ImageDataUrl = o.ImageDataUrl,
                        VoteCount = o.Votes.Count
                    }).ToList()
                })
                .ToList();
            foreach (var (question, source) in questions.Zip(election.Questions.OrderBy(q => q.DisplayOrder)))
            {
                question.TotalVotes = question.AllowMultipleAnswers
                    // A respondent can appear under several options here, so summing VoteCount
                    // would double-count them - count distinct respondents instead.
                    ? source.Options
                        .SelectMany(o => o.Votes)
                        .Select(v => (object?)v.UserId ?? v.VoteTokenId)
                        .Distinct()
                        .Count()
                    : question.Results.Sum(result => result.VoteCount);
            }

            if (questions.Count == 0)
            {
                var legacyResults = election.Options.Select(o => new OptionResultDto
                {
                    OptionId = o.Id,
                    Label = o.Label,
                    ImageDataUrl = o.ImageDataUrl,
                    VoteCount = o.Votes.Count
                }).ToList();
                questions.Add(new QuestionResultDto
                {
                    QuestionId = Guid.Empty,
                    Text = election.Question ?? election.Title,
                    TotalVotes = legacyResults.Sum(result => result.VoteCount),
                    Results = legacyResults
                });
            }

            return new ElectionResultsDto
            {
                ElectionId = election.Id,
                Title = election.Title,
                TotalVotes = questions.Max(q => q.TotalVotes),
                Results = questions[0].Results,
                Questions = questions
            };
        }

        public async Task<ElectionResultsDto?> GetResultsAsync(Guid electionId, Guid userId)
        {
            if (!await _elections.CanUserAccessAsync(electionId, userId))
                return null;

            return await GetResultsAsync(electionId);
        }
    }
}
