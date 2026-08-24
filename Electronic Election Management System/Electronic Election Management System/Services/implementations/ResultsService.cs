using Electronic_Election_Management_System.Constants;
using Electronic_Election_Management_System.Data.Repositories;
using Electronic_Election_Management_System.DTOs;
using Electronic_Election_Management_System.Models;

namespace Electronic_Election_Management_System.Services
{
    public interface IResultsService
    {
        Task<ElectionResultsDto?> GetResultsAsync(Guid electionId);
        Task<ElectionResultsDto?> GetResultsAsync(Guid electionId, Guid userId);
        Task<ServiceResult<List<OptionVotersDto>>> GetVotersAsync(Guid electionId, Guid? questionId, Guid requestedByUserId);
    }

    public class ResultsService : IResultsService
    {
        private readonly IElectionRepository _elections;
        private readonly IVoteRepository _votes;
        private readonly IUserRepository _users;

        public ResultsService(IElectionRepository elections, IVoteRepository votes, IUserRepository users)
        {
            _elections = elections;
            _votes = votes;
            _users = users;
        }

        /// <summary>
        /// Who picked one option, for a non-anonymous election only.
        /// </summary>
        /// <remarks>
        /// The anonymity check is not a formality: <c>Vote.VoteTokenId</c> leads to
        /// <c>VoteToken.UserId</c>, so an anonymous voter *is* reachable in the schema. The vote
        /// screen promises that identity is never linked to the chosen option, so this refuses
        /// outright rather than relying on the query to avoid following that link.
        /// </remarks>
        public async Task<ServiceResult<List<OptionVotersDto>>> GetVotersAsync(
            Guid electionId, Guid? questionId, Guid requestedByUserId)
        {
            var election = await _elections.GetByIdWithOptionsAsync(electionId);
            if (election is null)
                return ServiceResult<List<OptionVotersDto>>.NotFound();

            if (election.IsAnonymous)
                return ServiceResult<List<OptionVotersDto>>.Fail(ErrorCode.VotersHiddenForAnonymousElection);

            var requester = await _users.GetByIdAsync(requestedByUserId);

            // Holding the ElectionManager role elsewhere is not enough - it has to be this
            // election, so the check is against its creator rather than the role alone.
            var allowed = requester is not null &&
                (requester.Role == UserRole.Admin || election.CreatedByUserId == requestedByUserId);
            if (!allowed)
                return ServiceResult<List<OptionVotersDto>>.Fail(ErrorCode.NotAuthorizedToViewVoters);

            // A question id narrows this to that question. Without one we answer for the options
            // hanging off the election itself, which is how the older elections are shaped - they
            // have no ElectionQuestion rows at all.
            List<Option> options;
            if (questionId.HasValue)
            {
                var question = election.Questions.FirstOrDefault(q => q.Id == questionId.Value);
                if (question is null)
                    return ServiceResult<List<OptionVotersDto>>.NotFound();
                options = question.Options.ToList();
            }
            else
            {
                options = election.Options.Where(o => o.QuestionId is null).ToList();
            }

            var votes = await _votes.GetIdentifiedVotesForOptionsAsync(options.Select(o => o.Id));

            // One query for every voter's profile rather than one per person.
            var profileNames = (await _users.GetUserDetailsForUsersAsync(
                    votes.Where(v => v.UserId.HasValue).Select(v => v.UserId!.Value)))
                .Where(d => !string.IsNullOrWhiteSpace(d.FullName))
                .ToDictionary(d => d.UserId, d => d.FullName!.Trim());

            var castByOption = votes
                .Where(v => v.OptionId.HasValue && v.User is not null)
                .GroupBy(v => v.OptionId!.Value)
                .ToDictionary(g => g.Key, g => g.ToList());

            return ServiceResult<List<OptionVotersDto>>.Ok(options
                .Select(option => new OptionVotersDto
                {
                    OptionId = option.Id,
                    Label = option.Label,
                    Voters = (castByOption.TryGetValue(option.Id, out var cast) ? cast : [])
                        .Select(v => new OptionVoterDto
                        {
                            UserId = v.User!.Id,
                            Email = v.User.Email,
                            // The name declared for this vote wins over the account's: it is what
                            // the voter put their name to for this election, not whatever the
                            // profile happens to say today.
                            FullName = Blank(v.VoterDeclaration?.FullName)
                                ? profileNames.GetValueOrDefault(v.User.Id)
                                : v.VoterDeclaration!.FullName!.Trim()
                        })
                        .OrderBy(voter => voter.FullName ?? voter.Email, StringComparer.CurrentCultureIgnoreCase)
                        .ToList()
                })
                .ToList());
        }

        private static bool Blank(string? value) => string.IsNullOrWhiteSpace(value);

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
                    RequiredRankCount = q.RequiredRankCount,
                    Results = q.Options.Select(o => new OptionResultDto
                    {
                        OptionId = o.Id,
                        Label = o.Label,
                        ImageDataUrl = o.ImageDataUrl,
                        VoteCount = q.QuestionType == QuestionType.Ranking
                            ? o.Votes.Sum(v => GetRankingPoints(v.Rank, q.ScoringScheme, q.Options.Count))
                            : o.Votes.Count,
                        RankCounts = q.QuestionType == QuestionType.Ranking
                            ? o.Votes.Where(v => v.Rank.HasValue).GroupBy(v => v.Rank.Value).ToDictionary(g => g.Key, g => g.Count())
                            : null
                    }).ToList(),
                    ScoringScheme = q.ScoringScheme == null ? null : new ScoringSchemeDto
                    {
                        Id = q.ScoringScheme.Id,
                        Name = q.ScoringScheme.Name,
                        Points = q.ScoringScheme.Points ?? new List<int>(),
                        IsLinear = q.ScoringScheme.IsLinear,
                        IsPredefined = q.ScoringScheme.IsPredefined
                    },
                    // A FreeText question's answers, or a Choice question's "Other" answers.
                    TextAnswers = q.QuestionType == QuestionType.FreeText || q.AllowOtherOption
                        ? q.Votes.Where(v => v.AnswerText != null).Select(v => v.AnswerText!).ToList()
                        : new List<string>()
                })
                .ToList();
            foreach (var (question, source) in questions.Zip(election.Questions.OrderBy(q => q.DisplayOrder)))
            {
                // A Choice question's "Other" answers get their own synthetic entry in Results
                // (same shape as a real option) so the piechart/meter rings account for every
                // vote instead of only the fixed options - fixes the total-vs-chart mismatch.
                if (source.QuestionType == QuestionType.Choice && source.AllowOtherOption)
                {
                    question.Results.Add(new OptionResultDto
                    {
                        OptionId = Guid.Empty,
                        Label = "Other",
                        VoteCount = question.TextAnswers.Count,
                        IsOtherOption = true
                    });
                }

                if (source.QuestionType == QuestionType.FreeText)
                {
                    question.TotalVotes = question.TextAnswers.Count;
                }
                else if (question.AllowMultipleAnswers || source.QuestionType == QuestionType.Ranking)
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
                    // The "Other" entry added above is already part of Results, so summing it
                    // alone (no separate "+ TextAnswers.Count") avoids double-counting.
                    question.TotalVotes = question.Results.Sum(result => result.VoteCount);
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
                IsAnonymous = election.IsAnonymous,
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

        private static int GetRankingPoints(int? rank, ScoringScheme? scheme, int optionsCount)
        {
            if (!rank.HasValue) return 0;
            if (scheme == null)
            {
                return rank.Value switch
                {
                    1 => 12, 2 => 10, 3 => 8, 4 => 7, 5 => 6, 6 => 5, 7 => 4, 8 => 3, 9 => 2, 10 => 1, _ => 0
                };
            }

            if (scheme.IsLinear)
            {
                return Math.Max(0, optionsCount - rank.Value + 1);
            }

            if (scheme.Points != null && rank.Value > 0 && rank.Value <= scheme.Points.Count)
            {
                return scheme.Points[rank.Value - 1];
            }

            return 0;
        }
    }
}
