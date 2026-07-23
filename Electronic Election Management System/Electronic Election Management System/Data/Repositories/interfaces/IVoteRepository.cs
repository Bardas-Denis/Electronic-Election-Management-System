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

        /// <summary>
        /// True if this user has already voted in this election, regardless of whether it is
        /// anonymous (checks the issued VoteToken) or non-anonymous (checks the Vote record).
        /// </summary>
        Task<bool> HasUserVotedInElectionAsync(Guid userId, Guid electionId, bool isAnonymous);

        Task<bool> HasCnpBeenUsedInElectionAsync(string cnp, Guid electionId);

        /// <summary>True if at least one vote (from any voter) has been cast in this election. Used to lock editing.</summary>
        Task<bool> HasAnyVotesInElectionAsync(Guid electionId);

        Task AddVoteAsync(Vote vote);
        Task AddVoterDeclarationAsync(VoterDeclaration declaration);
        Task<List<Vote>> GetUserVotesInElectionAsync(Guid userId, Guid electionId);
        Task<VoteToken?> GetVoteTokenWithVotesAsync(Guid userId, Guid electionId);
        void RemoveVote(Vote vote);
        void RemoveVotes(IEnumerable<Vote> votes);

        /// <summary>
        /// Number of edit/delete changes the user has already made to their answer in this
        /// election. Persists independently of the Vote row so it survives deletion.
        /// </summary>
        Task<int> GetChangeCountAsync(Guid userId, Guid electionId);

        /// <summary>Records that the user just used one of their allowed changes (edit or delete).</summary>
        Task IncrementChangeCountAsync(Guid userId, Guid electionId);

        Task SaveChangesAsync();
    }
}
