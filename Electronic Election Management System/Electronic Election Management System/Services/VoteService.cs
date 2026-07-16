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
            var election = await _elections.GetByIdWithOptionsAsync(request.ElectionId);
            if (election is null)
                return ServiceResult<bool>.NotFound("Election not found.");

            if (!election.CanAcceptVotes())
                return ServiceResult<bool>.Fail("This election is not currently open for voting.");

            var option = election.Options.FirstOrDefault(o => o.Id == request.OptionId);
            if (option is null)
                return ServiceResult<bool>.Fail("The selected option does not belong to this election.");

            var result = election.IsAnonymous
                ? await CastAnonymousAsync(election.Id, option.Id, userId)
                : await CastIdentifiedAsync(election, option.Id, userId, request.VoterDeclaration);

            // Vote was recorded successfully - push the fresh tally to everyone watching this
            // election's live results dashboard.
            if (result.Success)
            {
                await BroadcastResultsAsync(election.Id);
            }

            return result;
        }

        private async Task BroadcastResultsAsync(Guid electionId)
        {
            var updated = await _results.GetResultsAsync(electionId);
            if (updated is not null)
            {
                await _resultsHub.Clients.Group(electionId.ToString())
                    .SendAsync("ResultsUpdated", updated);
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
                return ServiceResult<bool>.Fail("You have already voted in this election.");
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
                return ServiceResult<bool>.Fail("You have already voted in this election.");

            var declarationResult = BuildDeclaration(election.Type, declarationDto);
            if (!declarationResult.Success)
                return ServiceResult<bool>.Fail(declarationResult.Error!);

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
                return ServiceResult<VoterDeclaration>.Fail("Voter declaration is required for this election.");

            if (type == ElectionType.Politic)
            {
                if (string.IsNullOrWhiteSpace(dto.Cnp) ||
                    string.IsNullOrWhiteSpace(dto.FullName) ||
                    string.IsNullOrWhiteSpace(dto.DomiciliuJudet) ||
                    string.IsNullOrWhiteSpace(dto.DomiciliuAdresa))
                {
                    return ServiceResult<VoterDeclaration>.Fail(
                        "CNP, full name and domiciliu (județ + adresă) are required for Politic elections.");
                }

                // Gender/birth date are ALWAYS derived server-side from the CNP - never trust
                // whatever the client might send for those fields.
                var cnpInfo = _cnp.Parse(dto.Cnp);
                if (cnpInfo is null)
                    return ServiceResult<VoterDeclaration>.Fail("Invalid CNP.");

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