using Electronic_Election_Management_System.Constants;
using Electronic_Election_Management_System.Data.Repositories;
using Electronic_Election_Management_System.DTOs;
using Electronic_Election_Management_System.Hubs;
using Electronic_Election_Management_System.Models;
using Microsoft.AspNetCore.SignalR;

namespace Electronic_Election_Management_System.Services
{
    public class VoteService : IVoteService
    {
        private readonly IElectionRepository _elections;
        private readonly IVoteRepository _votes;
        private readonly ICnpService _cnp;
        private readonly IResultsService _results;
        private readonly IHubContext<ResultsHub> _resultsHub;
        private readonly ILogger<VoteService> _logger;

        public VoteService(
            IElectionRepository elections,
            IVoteRepository votes,
            ICnpService cnp,
            IResultsService results,
            IHubContext<ResultsHub> resultsHub,
            ILogger<VoteService> logger)
        {
            _elections = elections;
            _votes = votes;
            _cnp = cnp;
            _results = results;
            _resultsHub = resultsHub;
            _logger = logger;
        }

        public async Task<ServiceResult<bool>> CastVoteAsync(CastVoteRequest request, Guid userId)
        {
            var election = await _elections.GetAccessibleByIdWithOptionsAsync(request.ElectionId, userId);
            if (election is null)
                return ServiceResult<bool>.NotFound(ErrorCode.ResourceNotFound);

            if (!election.CanAcceptVotes())
                return ServiceResult<bool>.Fail(ErrorCode.ElectionNotOpen);

            var validated = ValidateAndBuildAnswers(election, request);
            if (validated is null)
                return ServiceResult<bool>.Fail(ErrorCode.InvalidOption);
            var (selected, textAnswers) = validated.Value;

            var result = election.IsAnonymous
                ? await CastAnonymousAsync(election.Id, selected, textAnswers, userId)
                : await CastIdentifiedAsync(election, selected, textAnswers, userId, request.VoterDeclaration);

            // Vote was recorded successfully - push the fresh tally to everyone watching this
            // election's live results dashboard.
            if (result.Success)
            {
                _logger.LogInformation(LogMessages.VoteCast, userId, election.Id);
                try
                {
                    await BroadcastResultsAsync(election.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, LogMessages.SignalRBroadcastFailed, election.Id);
                    // Broadcasting live results should not affect the vote-casting outcome.
                }
            }

            return result;
        }

        public async Task<ServiceResult<bool>> UpdateVoteAsync(CastVoteRequest request, Guid userId)
        {
            var election = await _elections.GetAccessibleByIdWithOptionsAsync(request.ElectionId, userId);
            if (election is null)
                return ServiceResult<bool>.NotFound(ErrorCode.ResourceNotFound);

            if (!election.CanAcceptVotes())
                return ServiceResult<bool>.Fail(ErrorCode.ElectionNotOpen);

            var validated = ValidateAndBuildAnswers(election, request);
            if (validated is null)
                return ServiceResult<bool>.Fail(ErrorCode.InvalidOption);
            var (selected, textAnswers) = validated.Value;

            var result = election.IsAnonymous
                ? await UpdateAnonymousAsync(election.Id, selected, textAnswers, userId)
                : await UpdateIdentifiedAsync(election, selected, textAnswers, userId, request.VoterDeclaration);

            if (result.Success)
            {
                _logger.LogInformation(LogMessages.VoteUpdated, userId, request.ElectionId);
                try
                {
                    await BroadcastResultsAsync(election.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, LogMessages.SignalRBroadcastFailed, election.Id);
                    // Broadcasting live results should not affect the vote-editing outcome.
                }
            }

            return result;
        }

        public async Task<ServiceResult<bool>> DeleteVoteAsync(Guid electionId, Guid userId)
        {
            var election = await _elections.GetAccessibleByIdWithOptionsAsync(electionId, userId);
            if (election is null)
                return ServiceResult<bool>.NotFound(ErrorCode.ResourceNotFound);

            if (!election.CanAcceptVotes())
                return ServiceResult<bool>.Fail(ErrorCode.ElectionNotOpen);

            var changeCount = await _votes.GetChangeCountAsync(userId, electionId);
            if (changeCount >= 1)
                return ServiceResult<bool>.Fail(ErrorCode.VoteChangeLimit);

            if (election.IsAnonymous)
            {
                var token = await _votes.GetVoteTokenWithVotesAsync(userId, electionId);
                if (token is null || token.Votes.Count == 0)
                    return ServiceResult<bool>.NotFound(ErrorCode.ResourceNotFound);

                _votes.RemoveVotes(token.Votes);
                // Free the token up so the voter can cast a fresh vote afterwards.
                token.IsUsed = false;
            }
            else
            {
                var votes = await _votes.GetUserVotesInElectionAsync(userId, electionId);
                if (votes.Count == 0)
                    return ServiceResult<bool>.NotFound(ErrorCode.ResourceNotFound);

                _votes.RemoveVotes(votes);
            }

            // Deleting counts as the one allowed change, same as editing - otherwise someone
            // could delete-and-revote in a loop to bypass the edit limit entirely.
            await _votes.IncrementChangeCountAsync(userId, electionId);
            await _votes.SaveChangesAsync();

            try
            {
                await BroadcastResultsAsync(electionId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, LogMessages.SignalRBroadcastFailed, electionId);
                // Broadcasting live results should not affect the vote-deletion outcome.
            }

            _logger.LogInformation(LogMessages.VoteDeleted, userId, electionId);

            return ServiceResult<bool>.Ok(true);
        }

        public async Task<ServiceResult<UserVoteDto>> GetMyVoteAsync(Guid electionId, Guid userId)
        {
            var election = await _elections.GetAccessibleByIdWithOptionsAsync(electionId, userId);
            if (election is null)
                return ServiceResult<UserVoteDto>.NotFound(ErrorCode.ResourceNotFound);

            List<Vote> votes;
            if (election.IsAnonymous)
            {
                var token = await _votes.GetVoteTokenWithVotesAsync(userId, electionId);
                votes = token?.Votes.ToList() ?? new List<Vote>();
            }
            else
            {
                votes = await _votes.GetUserVotesInElectionAsync(userId, electionId);
            }

            if (votes.Count == 0)
                return ServiceResult<UserVoteDto>.NotFound(ErrorCode.ResourceNotFound);

            var changeCount = await _votes.GetChangeCountAsync(userId, electionId);

            return ServiceResult<UserVoteDto>.Ok(new UserVoteDto
            {
                ElectionId = electionId,
                OptionId = votes[0].OptionId,
                OptionLabel = votes[0].Option?.Label,
                VotedAt = votes.Min(v => v.CastAt),
                Answers = votes.Select(v => new UserVoteAnswerDto
                {
                    QuestionId = v.OptionId.HasValue ? (v.Option?.QuestionId ?? Guid.Empty) : (v.QuestionId ?? Guid.Empty),
                    OptionId = v.OptionId,
                    OptionLabel = v.Option?.Label,
                    Text = v.AnswerText
                }).ToList(),
                CanEdit = changeCount < 1
            });
        }

        private async Task<ServiceResult<bool>> UpdateAnonymousAsync(
            Guid electionId, List<Option> selected, List<Vote> textAnswers, Guid userId)
        {
            var token = await _votes.GetVoteTokenWithVotesAsync(userId, electionId);
            if (token is null || token.Votes.Count == 0)
                return ServiceResult<bool>.Fail(ErrorCode.VoteNotFound);

            var changeCount = await _votes.GetChangeCountAsync(userId, electionId);
            if (changeCount >= 1)
                return ServiceResult<bool>.Fail(ErrorCode.VoteChangeLimit);

            _votes.RemoveVotes(token.Votes);
            foreach (var option in selected)
                await _votes.AddVoteAsync(new Vote { OptionId = option.Id, VoteTokenId = token.Id });
            foreach (var textAnswer in textAnswers)
            {
                textAnswer.VoteTokenId = token.Id;
                await _votes.AddVoteAsync(textAnswer);
            }
            await _votes.IncrementChangeCountAsync(userId, electionId);
            await _votes.SaveChangesAsync();

            return ServiceResult<bool>.Ok(true);
        }

        private async Task<ServiceResult<bool>> UpdateIdentifiedAsync(
            Election election, List<Option> selected, List<Vote> textAnswers, Guid userId, PersonalDetailsDto? declarationDto)
        {
            var existingVotes = await _votes.GetUserVotesInElectionAsync(userId, election.Id);
            if (existingVotes.Count == 0)
                return ServiceResult<bool>.Fail(ErrorCode.VoteNotFound);

            var changeCount = await _votes.GetChangeCountAsync(userId, election.Id);
            if (changeCount >= 1)
                return ServiceResult<bool>.Fail(ErrorCode.VoteChangeLimit);

            var declarationResult = BuildDeclaration(election.Type, declarationDto);
            if (!declarationResult.Success)
                return ServiceResult<bool>.Fail(declarationResult.ErrorCode!.Value);

            _votes.RemoveVotes(existingVotes);
            var newVotes = selected.Select(option => new Vote { OptionId = option.Id, UserId = userId }).ToList();
            foreach (var textAnswer in textAnswers)
                textAnswer.UserId = userId;
            newVotes.AddRange(textAnswers);
            foreach (var vote in newVotes)
                await _votes.AddVoteAsync(vote);
            var updated = declarationResult.Data!;
            updated.VoteId = newVotes[0].Id;
            await _votes.AddVoterDeclarationAsync(updated);

            await _votes.IncrementChangeCountAsync(userId, election.Id);
            await _votes.SaveChangesAsync();
            return ServiceResult<bool>.Ok(true);
        }

        private async Task BroadcastResultsAsync(Guid electionId)
        {
            var updated = await _results.GetResultsAsync(electionId);
            if (updated is not null)
            {
                await _resultsHub.Clients.Group(electionId.ToString())
                    .SendAsync(HubEvents.ResultsUpdated, updated);
            }
        }

        /// <summary>Validates a full <see cref="CastVoteRequest"/> against the election's
        /// questions and builds the unsaved answer rows for it: one <see cref="Option"/> per
        /// selected choice, plus one text-answer <see cref="Vote"/> per non-blank text answer
        /// (for FreeText questions, and for Choice questions with <see cref="ElectionQuestion.AllowOtherOption"/>).
        /// Voter identity fields are filled in by the caller. Returns <c>null</c> if anything
        /// doesn't add up: an unknown option/question id, a text answer for a question that
        /// doesn't accept one, or a required/single-answer count violation - a text answer
        /// counts as one answer alongside any selected options for that question.</summary>
        private static (List<Option> Options, List<Vote> TextAnswers)? ValidateAndBuildAnswers(
            Election election, CastVoteRequest request)
        {
            var requestedOptionIds = (request.OptionIds.Count > 0
                    ? request.OptionIds
                    : request.OptionId == Guid.Empty ? new List<Guid>() : new List<Guid> { request.OptionId })
                .Distinct()
                .ToList();
            var selectedOptions = election.Options.Where(option => requestedOptionIds.Contains(option.Id)).ToList();
            if (selectedOptions.Count != requestedOptionIds.Count)
                return null;

            var questions = election.Questions.ToList();
            if (questions.Count == 0)
            {
                // Legacy single-question election (no ElectionQuestion rows) - always required,
                // never has text answers.
                if (request.TextAnswers.Count > 0)
                    return null;
                return selectedOptions.Count == 1 && selectedOptions[0].QuestionId is null
                    ? (selectedOptions, new List<Vote>())
                    : null;
            }

            // Every selected option must belong to one of this election's questions.
            var knownQuestionIds = questions.Select(question => question.Id).ToHashSet();
            if (!selectedOptions.All(option => option.QuestionId.HasValue && knownQuestionIds.Contains(option.QuestionId.Value)))
                return null;

            // Text answers are accepted for FreeText questions, and for Choice questions that
            // allow an "Other" answer alongside/instead of picking a fixed option.
            var textEligibleQuestionIds = questions
                .Where(question => question.QuestionType == QuestionType.FreeText || question.AllowOtherOption)
                .Select(question => question.Id)
                .ToHashSet();
            var answeredQuestionIds = request.TextAnswers.Select(answer => answer.QuestionId).ToList();
            if (answeredQuestionIds.Count != answeredQuestionIds.Distinct().Count() ||
                answeredQuestionIds.Any(id => !textEligibleQuestionIds.Contains(id)))
                return null;

            var textByQuestionId = request.TextAnswers.ToDictionary(
                answer => answer.QuestionId,
                answer => answer.Text?.Trim() ?? string.Empty);

            // Single-answer questions: required needs exactly one answer (option or text),
            // optional accepts zero or one. Multiple-answer questions: required needs at least
            // one, optional accepts any count.
            var countsValid = questions.All(question =>
            {
                var optionCount = selectedOptions.Count(option => option.QuestionId == question.Id);
                var hasTextAnswer = textByQuestionId.TryGetValue(question.Id, out var text) &&
                    !string.IsNullOrWhiteSpace(text);
                var effectiveCount = optionCount + (hasTextAnswer ? 1 : 0);
                return question.AllowMultipleAnswers
                    ? !question.IsRequired || effectiveCount >= 1
                    : question.IsRequired ? effectiveCount == 1 : effectiveCount <= 1;
            });
            if (!countsValid)
                return null;

            var textAnswerVotes = questions
                .Where(question => textByQuestionId.TryGetValue(question.Id, out var text) && !string.IsNullOrWhiteSpace(text))
                .Select(question => new Vote { QuestionId = question.Id, AnswerText = textByQuestionId[question.Id] })
                .ToList();

            return (selectedOptions, textAnswerVotes);
        }

        private async Task<ServiceResult<bool>> CastAnonymousAsync(
            Guid electionId, List<Option> selected, List<Vote> textAnswers, Guid userId)
        {
            var token = await _votes.GetVoteTokenAsync(userId, electionId);
            if (token is null)
            {
                token = new VoteToken { UserId = userId, ElectionId = electionId };
                await _votes.AddVoteTokenAsync(token);
            }
            else if (token.IsUsed)
            {
                return ServiceResult<bool>.Fail(ErrorCode.AlreadyVoted);
            }

            token.IsUsed = true;

            // Anonymous path: only VoteTokenId is set, no UserId and no VoterDeclaration - the
            // vote can never be traced back to who cast it.
            foreach (var option in selected)
                await _votes.AddVoteAsync(new Vote { OptionId = option.Id, VoteTokenId = token.Id });
            foreach (var textAnswer in textAnswers)
            {
                textAnswer.VoteTokenId = token.Id;
                await _votes.AddVoteAsync(textAnswer);
            }
            await _votes.SaveChangesAsync();

            return ServiceResult<bool>.Ok(true);
        }

        private async Task<ServiceResult<bool>> CastIdentifiedAsync(
            Election election, List<Option> selected, List<Vote> textAnswers, Guid userId, PersonalDetailsDto? declarationDto)
        {
            if (await _votes.HasUserVotedAsync(userId, election.Id))
                return ServiceResult<bool>.Fail(ErrorCode.AlreadyVoted);

            var declarationResult = BuildDeclaration(election.Type, declarationDto);
            if (!declarationResult.Success)
                return ServiceResult<bool>.Fail(declarationResult.ErrorCode!.Value);

            var votes = selected.Select(option => new Vote { OptionId = option.Id, UserId = userId }).ToList();
            foreach (var textAnswer in textAnswers)
                textAnswer.UserId = userId;
            votes.AddRange(textAnswers);
            foreach (var vote in votes)
                await _votes.AddVoteAsync(vote);

            var declaration = declarationResult.Data!;
            declaration.VoteId = votes[0].Id;
            await _votes.AddVoterDeclarationAsync(declaration);

            await _votes.SaveChangesAsync();
            return ServiceResult<bool>.Ok(true);
        }

        /// <summary>Validates and builds the VoterDeclaration expected for the election's Type.</summary>
        private ServiceResult<VoterDeclaration> BuildDeclaration(ElectionType type, PersonalDetailsDto? dto)
        {
            if (dto is null)
                return ServiceResult<VoterDeclaration>.Fail(ErrorCode.DeclarationRequired);

            if (type == ElectionType.Politic)
            {
                if (string.IsNullOrWhiteSpace(dto.Cnp) ||
                    string.IsNullOrWhiteSpace(dto.FullName) ||
                    string.IsNullOrWhiteSpace(dto.ResidenceCounty) ||
                    string.IsNullOrWhiteSpace(dto.ResidenceAddress))
                {
                    return ServiceResult<VoterDeclaration>.Fail(ErrorCode.IncompleteDeclaration);
                }

                // Gender/birth date are ALWAYS derived server-side from the CNP - never trust
                // whatever the client might send for those fields.
                var cnpInfo = _cnp.Parse(dto.Cnp);
                if (cnpInfo is null)
                    return ServiceResult<VoterDeclaration>.Fail(ErrorCode.InvalidCnp);

                var politic = new VoterDeclaration
                {
                    BirthDate = cnpInfo.BirthDate,
                    Gender = cnpInfo.Gender
                };
                PersonalDetailsMapper.ApplyTrimmed(dto, politic);
                // Overwrite Gender with the CNP-derived value (ApplyTrimmed would have set the
                // client-supplied value, but for Politic elections it must always come from the CNP).
                politic.Gender = cnpInfo.Gender;
                return ServiceResult<VoterDeclaration>.Ok(politic);
            }

            // Comercial
            var comercial = new VoterDeclaration();
            PersonalDetailsMapper.ApplyTrimmed(dto, comercial);
            return ServiceResult<VoterDeclaration>.Ok(comercial);
        }
    }
}
