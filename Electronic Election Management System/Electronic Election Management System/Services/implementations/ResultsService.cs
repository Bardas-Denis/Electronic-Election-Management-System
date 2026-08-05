using Electronic_Election_Management_System.Data.Repositories;
using Electronic_Election_Management_System.DTOs;
using Electronic_Election_Management_System.Models;

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
                    QuestionType = q.QuestionType.ToString(),
                    Results = q.Options.Select(o => new OptionResultDto
                    {
                        OptionId = o.Id,
                        Label = o.Label,
                        ImageDataUrl = o.ImageDataUrl,
                        VoteCount = o.Votes.Count
                    }).ToList(),
                    // A FreeText question's answers, or a Choice question's "Other" answers.
                    TextAnswers = q.QuestionType == QuestionType.FreeText || q.AllowOtherOption
                        ? q.Votes.Where(v => v.AnswerText != null).Select(v => v.AnswerText!).ToList()
                        : new List<string>()
                })
                .ToList();
            foreach (var (question, source) in questions.Zip(election.Questions.OrderBy(q => q.DisplayOrder)))
            {
                if (source.QuestionType == QuestionType.FreeText)
                {
                    question.TotalVotes = question.TextAnswers.Count;
                }
                else if (question.AllowMultipleAnswers)
                {
                    // A respondent can appear under several options here, so summing VoteCount
                    // would double-count them - count distinct respondents instead. A respondent
                    // who only used "Other" (no fixed option) still needs to be counted once.
                    var optionRespondents = source.Options
                        .SelectMany(o => o.Votes)
                        .Select(v => (object?)v.UserId ?? v.VoteTokenId);
                    var otherRespondents = source.Votes
                        .Where(v => v.AnswerText != null)
                        .Select(v => (object?)v.UserId ?? v.VoteTokenId);
                    question.TotalVotes = optionRespondents.Concat(otherRespondents).Distinct().Count();
                }
                else
                {
                    // Single-answer: every option pick or "Other" answer is its own respondent.
                    question.TotalVotes = question.Results.Sum(result => result.VoteCount) + question.TextAnswers.Count;
                }
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
                    QuestionType = QuestionType.Choice.ToString(),
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
