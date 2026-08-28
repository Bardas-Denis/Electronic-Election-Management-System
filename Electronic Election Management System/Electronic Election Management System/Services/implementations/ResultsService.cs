using Electronic_Election_Management_System.Constants;
using Electronic_Election_Management_System.Data.Repositories;
using Electronic_Election_Management_System.DTOs;
using Electronic_Election_Management_System.Models;
using Electronic_Election_Management_System.Plugins;
using Eems.PluginContracts;

namespace Electronic_Election_Management_System.Services
{
    public interface IResultsService
    {
        Task<ElectionResultsDto?> GetResultsAsync(Guid electionId);
        Task<ElectionResultsDto?> GetResultsAsync(Guid electionId, Guid userId);
        Task<ServiceResult<List<OptionVotersDto>>> GetVotersAsync(Guid electionId, Guid? questionId, Guid requestedByUserId);
        Task<ServiceResult<List<TextAnswerAuthorDto>>> GetTextAnswerAuthorsAsync(Guid electionId, Guid questionId, Guid requestedByUserId);
    }

    public class ResultsService : IResultsService
    {
        private readonly IElectionRepository _elections;
        private readonly IVoteRepository _votes;
        private readonly IUserRepository _users;
        private readonly IScoringPluginRegistry _plugins;
        private readonly ILogger<ResultsService> _logger;

        public ResultsService(
            IElectionRepository elections,
            IVoteRepository votes,
            IUserRepository users,
            IScoringPluginRegistry plugins,
            ILogger<ResultsService> logger)
        {
            _elections = elections;
            _votes = votes;
            _users = users;
            _plugins = plugins;
            _logger = logger;
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
            var access = await AuthorizeVoterLookupAsync(electionId, requestedByUserId);
            if (!access.Success)
                return access.IsNotFound
                    ? ServiceResult<List<OptionVotersDto>>.NotFound(access.ErrorCode!.Value)
                    : ServiceResult<List<OptionVotersDto>>.Fail(access.ErrorCode!.Value);

            var election = access.Data!;

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
            var profileNames = await ProfileNamesForAsync(votes);

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
                            FullName = ResolveVoterName(v, profileNames)
                        })
                        .OrderBy(voter => voter.FullName ?? voter.Email, StringComparer.CurrentCultureIgnoreCase)
                        .ToList()
                })
                .ToList());
        }

        /// <summary>
        /// The single gate every "who was behind this" lookup goes through.
        /// </summary>
        /// <remarks>
        /// Kept in one place deliberately: the anonymity refusal is the security-critical rule
        /// here, and two copies of it in two endpoints are two chances for one to drift. The
        /// check is not a formality either - <c>Vote.VoteTokenId</c> leads to
        /// <c>VoteToken.UserId</c>, so an anonymous voter *is* reachable in the schema, and the
        /// vote screen promises they are not.
        /// </remarks>
        private async Task<ServiceResult<Election>> AuthorizeVoterLookupAsync(Guid electionId, Guid requestedByUserId)
        {
            var election = await _elections.GetByIdWithOptionsAsync(electionId);
            if (election is null)
                return ServiceResult<Election>.NotFound();

            if (election.IsAnonymous)
                return ServiceResult<Election>.Fail(ErrorCode.VotersHiddenForAnonymousElection);

            var requester = await _users.GetByIdAsync(requestedByUserId);

            // Holding the ElectionManager role elsewhere is not enough - it has to be this
            // election, so the check is against its creator rather than the role alone.
            var allowed = requester is not null &&
                (requester.Role == UserRole.Admin || election.CreatedByUserId == requestedByUserId);

            return allowed
                ? ServiceResult<Election>.Ok(election)
                : ServiceResult<Election>.Fail(ErrorCode.NotAuthorizedToViewVoters);
        }

        /// <summary>
        /// Every typed answer on one question together with who wrote it, for a non-anonymous
        /// election only. Text and author travel as a pair rather than being matched back to the
        /// results payload by position - that payload sends answers as bare strings, so two
        /// identical answers cannot be told apart there.
        /// </summary>
        public async Task<ServiceResult<List<TextAnswerAuthorDto>>> GetTextAnswerAuthorsAsync(
            Guid electionId, Guid questionId, Guid requestedByUserId)
        {
            var access = await AuthorizeVoterLookupAsync(electionId, requestedByUserId);
            if (!access.Success)
                return access.IsNotFound
                    ? ServiceResult<List<TextAnswerAuthorDto>>.NotFound(access.ErrorCode!.Value)
                    : ServiceResult<List<TextAnswerAuthorDto>>.Fail(access.ErrorCode!.Value);

            if (access.Data!.Questions.All(q => q.Id != questionId))
                return ServiceResult<List<TextAnswerAuthorDto>>.NotFound();

            var votes = await _votes.GetIdentifiedTextAnswersForQuestionAsync(questionId);

            var profileNames = await ProfileNamesForAsync(votes);

            return ServiceResult<List<TextAnswerAuthorDto>>.Ok(votes
                .Where(v => v.User is not null)
                .Select(v => new TextAnswerAuthorDto
                {
                    AnswerText = v.AnswerText ?? string.Empty,
                    UserId = v.User!.Id,
                    Email = v.User.Email,
                    FullName = ResolveVoterName(v, profileNames)
                })
                .ToList());
        }

        /// <summary>
        /// The name to show for a vote: the account's, falling back to whatever was declared for
        /// the vote itself. The account name is the one the person is known by across the system,
        /// and it is the only one present on every election - a declaration is only collected on
        /// Politic ones, and even there it carries the legal name rather than the working one.
        /// Null when neither exists, and the caller falls back to the email.
        /// </summary>
        private static string? ResolveVoterName(Vote vote, IReadOnlyDictionary<Guid, string> profileNames)
        {
            var profile = vote.UserId.HasValue ? profileNames.GetValueOrDefault(vote.UserId.Value) : null;
            if (!string.IsNullOrWhiteSpace(profile))
                return profile;

            var declared = vote.VoterDeclaration?.FullName;
            return string.IsNullOrWhiteSpace(declared) ? null : declared.Trim();
        }

        /// <summary>Profile names for a set of voters, fetched in one query rather than one per
        /// person, keyed by user id and stripped of blanks.</summary>
        private async Task<Dictionary<Guid, string>> ProfileNamesForAsync(IEnumerable<Vote> votes)
            => (await _users.GetUserDetailsForUsersAsync(
                    votes.Where(v => v.UserId.HasValue).Select(v => v.UserId!.Value)))
                .Where(d => !string.IsNullOrWhiteSpace(d.FullName))
                .ToDictionary(d => d.UserId, d => d.FullName!.Trim());

        public async Task<ElectionResultsDto?> GetResultsAsync(Guid electionId)
        {
            var election = await _elections.GetByIdWithResultsAsync(electionId);
            if (election is null)
                return null;

            // One scorer per question, resolved before any ballot is counted: a missing or
            // misbehaving plugin then costs a single log line rather than one per vote per option.
            var orderedQuestions = election.Questions.OrderBy(q => q.DisplayOrder).ToList();
            var scorers = orderedQuestions.ToDictionary(
                q => q.Id,
                q => ResolveRankingScorer(q.ScoringScheme, q.Options.Count));

            var questions = orderedQuestions
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
                        ImageId = o.ImageId,
                        VoteCount = q.QuestionType == QuestionType.Ranking
                            ? o.Votes.Sum(v => scorers[q.Id](v.Rank))
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
                        IsPredefined = q.ScoringScheme.IsPredefined,
                        PluginKey = q.ScoringScheme.PluginKey
                    },
                    // A FreeText question's answers, or a Choice question's "Other" answers.
                    // Ordered by when they were cast, and deliberately by the same key the
                    // text-answer-authors endpoint uses: the dashboard swaps one list for the
                    // other when the authors are revealed, and without a shared ordering the
                    // answers would visibly rearrange themselves at that moment.
                    // The id breaks ties: two answers landing in the same tick would otherwise
                    // be ordered however the database felt like it, differently in each of the
                    // two queries, and the list would shuffle on reveal for those rows alone.
                    TextAnswers = q.QuestionType == QuestionType.FreeText || q.AllowOtherOption
                        ? q.Votes.Where(v => v.AnswerText != null)
                            .OrderBy(v => v.CastAt)
                            .ThenBy(v => v.Id)
                            .Select(v => v.AnswerText!)
                            .ToList()
                        : new List<string>()
                })
                .ToList();
            foreach (var (question, source) in questions.Zip(election.Questions.OrderBy(q => q.DisplayOrder)))
            {
                // "Other" answers get a synthetic entry shaped like a real option, so the charts
                // account for every vote rather than only the fixed ones.
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
                    ImageId = o.ImageId,
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

        /// <summary>
        /// Picks the points function for one ranked question, once, before any ballot is counted.
        /// </summary>
        /// <remarks>
        /// A scheme carrying a <see cref="ScoringScheme.PluginKey"/> defers to that plugin; every
        /// other scheme keeps the built-in behaviour untouched.
        /// </remarks>
        private Func<int?, int> ResolveRankingScorer(ScoringScheme? scheme, int optionsCount)
        {
            if (scheme?.PluginKey is not { Length: > 0 } key)
            {
                return rank => GetRankingPoints(rank, scheme, optionsCount);
            }

            if (!_plugins.TryGet(key, out var plugin))
            {
                // Linear keeps the ranking order meaningful. Scoring everything 0 would render the
                // election as a perfect tie, which reads as a result rather than as a fault.
                _logger.LogWarning(
                    "Scoring scheme {Scheme} needs plugin {Key}, which is not loaded. "
                    + "Falling back to linear scoring.", scheme!.Name, key);

                return rank => rank.HasValue ? Math.Max(0, optionsCount - rank.Value + 1) : 0;
            }

            var faulted = false;
            return rank =>
            {
                if (!rank.HasValue) return 0;

                try
                {
                    return plugin.GetPoints(
                        new RankingContext { Rank = rank.Value, OptionsCount = optionsCount });
                }
                catch (Exception ex)
                {
                    // Plugin code is not ours; one throwing must not take down the results page.
                    if (!faulted)
                    {
                        faulted = true;
                        _logger.LogError(ex,
                            "Scoring plugin {Key} threw while scoring a rank; counting it as 0.",
                            key);
                    }

                    return 0;
                }
            };
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
