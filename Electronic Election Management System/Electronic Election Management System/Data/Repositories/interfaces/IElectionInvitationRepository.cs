using Electronic_Election_Management_System.Models;

namespace Electronic_Election_Management_System.Data.Repositories
{
    public interface IElectionInvitationRepository
    {
        Task<List<ElectionInvitation>> GetByElectionAsync(Guid electionId);
        Task<ElectionInvitation?> GetByIdAsync(Guid invitationId);
        Task<List<string>> GetExistingEmailsAsync(Guid electionId, IEnumerable<string> emails);
        Task AddRangeAsync(IEnumerable<ElectionInvitation> invitations);
        void Remove(ElectionInvitation invitation);
        Task SaveChangesAsync();
    }
}
