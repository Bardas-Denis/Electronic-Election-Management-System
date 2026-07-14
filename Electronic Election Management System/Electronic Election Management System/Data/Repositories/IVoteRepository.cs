using Electronic_Election_Management_System.Models;

namespace Electronic_Election_Management_System.Data.Repositories
{
    public interface IVoteRepository
    {
        /// <summary>Gets the (user, election) VoteToken if one has already been issued, used for anonymous elections.</summary>
        Task<VoteToken?> GetVoteTokenAsync(Guid userId, Guid electionId);
        Task AddVoteTokenAsync(VoteToken token);

        /// <summary>True if this user already has a Vote recorded for this election (non-anonymous path only).</summary>
        Task<bool> HasUserVotedAsync(Guid userId, Guid electionId);

        Task<bool> HasCnpBeenUsedInElectionAsync(string cnp, Guid electionId);

        Task AddVoteAsync(Vote vote);
        Task AddVoterDeclarationAsync(VoterDeclaration declaration);

        Task SaveChangesAsync();
    }
}
