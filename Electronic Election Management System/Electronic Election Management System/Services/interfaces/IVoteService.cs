using Electronic_Election_Management_System.DTOs;

namespace Electronic_Election_Management_System.Services
{
    public interface IVoteService
    {
        /// <summary>
        /// Casts a vote for the current user. Handles both the anonymous path (VoteToken, no
        /// declaration stored) and the non-anonymous path (UserId + type-dependent VoterDeclaration).
        /// </summary>
        Task<ServiceResult<bool>> CastVoteAsync(CastVoteRequest request, Guid userId);
    }
}
