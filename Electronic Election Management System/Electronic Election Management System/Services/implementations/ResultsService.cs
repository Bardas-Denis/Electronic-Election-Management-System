using Electronic_Election_Management_System.Data.Repositories;
using Electronic_Election_Management_System.DTOs;

namespace Electronic_Election_Management_System.Services
{
    public interface IResultsService
    {
        Task<ElectionResultsDto?> GetResultsAsync(Guid electionId);
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

            var optionResults = election.Options
                .Select(o => new OptionResultDto
                {
                    OptionId = o.Id,
                    Label = o.Label,
                    VoteCount = o.Votes.Count
                })
                .ToList();

            return new ElectionResultsDto
            {
                ElectionId = election.Id,
                Title = election.Title,
                TotalVotes = optionResults.Sum(r => r.VoteCount),
                Results = optionResults
            };
        }
    }
}