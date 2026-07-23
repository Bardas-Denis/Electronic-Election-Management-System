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

        public VoteService(
            IElectionRepository elections,
            IVoteRepository votes,
            ICnpService cnp,
            IResultsService results,
            IHubContext<ResultsHub> resultsHub)
        {
            _elections = elections;
            _votes = votes;
            _cnp = cnp;
            _results = results;
            _resultsHub = resultsHub;
        }

        public async Task<ServiceResult<bool>> CastVoteAsync(CastVoteRequest request, Guid userId)
        {
            var election = await _elections.GetAccessibleByIdWithOptionsAsync(request.ElectionId, userId);
            if (election is null)
                return ServiceResult<bool>.NotFound(ErrorCode.ResourceNotFound);

            if (!election.CanAcceptVotes())
                return ServiceResult<bool>.Fail(ErrorCode.ElectionNotOpen);

            var option = election.Options.FirstOrDefault(o => o.Id == request.OptionId);
            if (option is null)
                return ServiceResult<bool>.Fail(ErrorCode.InvalidOption);

            var result = election.IsAnonymous
                ? await CastAnonymousAsync(election.Id, option.Id, userId)
                : await CastIdentifiedAsync(election, option.Id, userId, request.VoterDeclaration);

            // Vote was recorded successfully - push the fresh tally to everyone watching this
            // election's live results dashboard.
            if (result.Success)
            {
                try
                {
                    await BroadcastResultsAsync(election.Id);
                }
                catch
                {
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

            var option = election.Options.FirstOrDefault(o => o.Id == request.OptionId);
            if (option is null)
                return ServiceResult<bool>.Fail(ErrorCode.InvalidOption);

            var result = election.IsAnonymous
                ? await UpdateAnonymousAsync(election.Id, option.Id, userId)
                : await UpdateIdentifiedAsync(election, option.Id, userId, request.VoterDeclaration);

            if (result.Success)
            {
                try
                {
                    await BroadcastResultsAsync(election.Id);
                }
                catch
                {
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
                var token = await _votes.GetVoteTokenWithVoteAsync(userId, electionId);
                if (token?.Vote is null)
                    return ServiceResult<bool>.NotFound(ErrorCode.ResourceNotFound);

                _votes.RemoveVote(token.Vote);
                // Free the token up so the voter can cast a fresh vote afterwards.
                token.IsUsed = false;
            }
            else
            {
                var vote = await _votes.GetUserVoteInElectionAsync(userId, electionId);
                if (vote is null)
                    return ServiceResult<bool>.NotFound(ErrorCode.ResourceNotFound);

                _votes.RemoveVote(vote);
            }

            // Deleting counts as the one allowed change, same as editing - otherwise someone
            // could delete-and-revote in a loop to bypass the edit limit entirely.
            await _votes.IncrementChangeCountAsync(userId, electionId);
            await _votes.SaveChangesAsync();

            try
            {
                await BroadcastResultsAsync(electionId);
            }
            catch
            {
                // Broadcasting live results should not affect the vote-deletion outcome.
            }

            return ServiceResult<bool>.Ok(true);
        }

        public async Task<ServiceResult<UserVoteDto>> GetMyVoteAsync(Guid electionId, Guid userId)
        {
            var election = await _elections.GetAccessibleByIdWithOptionsAsync(electionId, userId);
            if (election is null)
                return ServiceResult<UserVoteDto>.NotFound(ErrorCode.ResourceNotFound);

            Vote? vote;
            if (election.IsAnonymous)
            {
                var token = await _votes.GetVoteTokenWithVoteAsync(userId, electionId);
                vote = token?.Vote;
            }
            else
            {
                vote = await _votes.GetUserVoteInElectionAsync(userId, electionId);
            }

            if (vote is null)
                return ServiceResult<UserVoteDto>.NotFound(ErrorCode.ResourceNotFound);

            var changeCount = await _votes.GetChangeCountAsync(userId, electionId);

            return ServiceResult<UserVoteDto>.Ok(new UserVoteDto
            {
                ElectionId = electionId,
                OptionId = vote.OptionId,
                OptionLabel = vote.Option?.Label,
                VotedAt = vote.CastAt,
                CanEdit = changeCount < 1
            });
        }

        private async Task<ServiceResult<bool>> UpdateAnonymousAsync(Guid electionId, Guid optionId, Guid userId)
        {
            var token = await _votes.GetVoteTokenWithVoteAsync(userId, electionId);
            if (token?.Vote is null)
                return ServiceResult<bool>.Fail(ErrorCode.VoteNotFound);

            var changeCount = await _votes.GetChangeCountAsync(userId, electionId);
            if (changeCount >= 1)
                return ServiceResult<bool>.Fail(ErrorCode.VoteChangeLimit);

            token.Vote.OptionId = optionId;
            await _votes.IncrementChangeCountAsync(userId, electionId);
            await _votes.SaveChangesAsync();

            return ServiceResult<bool>.Ok(true);
        }

        private async Task<ServiceResult<bool>> UpdateIdentifiedAsync(
            Election election, Guid optionId, Guid userId, VoterDeclarationDto? declarationDto)
        {
            var vote = await _votes.GetUserVoteInElectionAsync(userId, election.Id);
            if (vote is null)
                return ServiceResult<bool>.Fail(ErrorCode.VoteNotFound);

            var changeCount = await _votes.GetChangeCountAsync(userId, election.Id);
            if (changeCount >= 1)
                return ServiceResult<bool>.Fail(ErrorCode.VoteChangeLimit);

            var declarationResult = BuildDeclaration(election.Type, declarationDto);
            if (!declarationResult.Success)
                return ServiceResult<bool>.Fail(declarationResult.ErrorCode!.Value);

            vote.OptionId = optionId;

            var updated = declarationResult.Data!;
            if (vote.VoterDeclaration is not null)
            {
                var existing = vote.VoterDeclaration;
                existing.Cnp = updated.Cnp;
                existing.FullName = updated.FullName;
                existing.DomiciliuJudet = updated.DomiciliuJudet;
                existing.DomiciliuAdresa = updated.DomiciliuAdresa;
                existing.DomiciliuLocalitate = updated.DomiciliuLocalitate;
                existing.Citizenship = updated.Citizenship;
                existing.BirthDate = updated.BirthDate;
                existing.Gender = updated.Gender;
                existing.EmployeeId = updated.EmployeeId;
                existing.Department = updated.Department;
                existing.WorkEmail = updated.WorkEmail;
                existing.JobTitle = updated.JobTitle;
                existing.Company = updated.Company;
            }
            else
            {
                updated.VoteId = vote.Id;
                await _votes.AddVoterDeclarationAsync(updated);
            }

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

        private async Task<ServiceResult<bool>> CastAnonymousAsync(Guid electionId, Guid optionId, Guid userId)
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
            await _votes.AddVoteAsync(new Vote { OptionId = optionId, VoteTokenId = token.Id });
            await _votes.SaveChangesAsync();

            return ServiceResult<bool>.Ok(true);
        }

        private async Task<ServiceResult<bool>> CastIdentifiedAsync(
            Election election, Guid optionId, Guid userId, VoterDeclarationDto? declarationDto)
        {
            if (await _votes.HasUserVotedAsync(userId, election.Id))
                return ServiceResult<bool>.Fail(ErrorCode.AlreadyVoted);

            var declarationResult = BuildDeclaration(election.Type, declarationDto);
            if (!declarationResult.Success)
                return ServiceResult<bool>.Fail(declarationResult.ErrorCode!.Value);

            var vote = new Vote { OptionId = optionId, UserId = userId };
            await _votes.AddVoteAsync(vote);

            var declaration = declarationResult.Data!;
            declaration.VoteId = vote.Id;
            await _votes.AddVoterDeclarationAsync(declaration);

            await _votes.SaveChangesAsync();
            return ServiceResult<bool>.Ok(true);
        }

        /// <summary>Validates and builds the VoterDeclaration expected for the election's Type.</summary>
        private ServiceResult<VoterDeclaration> BuildDeclaration(ElectionType type, VoterDeclarationDto? dto)
        {
            if (dto is null)
                return ServiceResult<VoterDeclaration>.Fail(ErrorCode.DeclarationRequired);

            if (type == ElectionType.Politic)
            {
                if (string.IsNullOrWhiteSpace(dto.Cnp) ||
                    string.IsNullOrWhiteSpace(dto.FullName) ||
                    string.IsNullOrWhiteSpace(dto.DomiciliuJudet) ||
                    string.IsNullOrWhiteSpace(dto.DomiciliuAdresa))
                {
                    return ServiceResult<VoterDeclaration>.Fail(ErrorCode.IncompleteDeclaration);
                }

                // Gender/birth date are ALWAYS derived server-side from the CNP - never trust
                // whatever the client might send for those fields.
                var cnpInfo = _cnp.Parse(dto.Cnp);
                if (cnpInfo is null)
                    return ServiceResult<VoterDeclaration>.Fail(ErrorCode.InvalidCnp);

                return ServiceResult<VoterDeclaration>.Ok(new VoterDeclaration
                {
                    Cnp = dto.Cnp,
                    FullName = dto.FullName.Trim(),
                    DomiciliuJudet = dto.DomiciliuJudet.Trim(),
                    DomiciliuAdresa = dto.DomiciliuAdresa.Trim(),
                    BirthDate = cnpInfo.BirthDate,
                    Gender = cnpInfo.Gender
                });
            }

            // Comercial
            return ServiceResult<VoterDeclaration>.Ok(new VoterDeclaration
            {
                Gender = string.IsNullOrWhiteSpace(dto.Gender) ? null : dto.Gender.Trim(),
                FullName = string.IsNullOrWhiteSpace(dto.FullName) ? null : dto.FullName.Trim(),
                WorkEmail = string.IsNullOrWhiteSpace(dto.WorkEmail) ? null : dto.WorkEmail.Trim(),
                Department = string.IsNullOrWhiteSpace(dto.Department) ? null : dto.Department.Trim(),
                JobTitle = string.IsNullOrWhiteSpace(dto.JobTitle) ? null : dto.JobTitle.Trim(),
                Company = string.IsNullOrWhiteSpace(dto.Company) ? null : dto.Company.Trim(),
                EmployeeId = string.IsNullOrWhiteSpace(dto.EmployeeId) ? null : dto.EmployeeId.Trim()
            });
        }
    }
}
