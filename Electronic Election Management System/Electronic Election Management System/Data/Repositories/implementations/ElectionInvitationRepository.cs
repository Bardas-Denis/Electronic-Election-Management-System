using Electronic_Election_Management_System.Models;
using Microsoft.EntityFrameworkCore;

namespace Electronic_Election_Management_System.Data.Repositories
{
    public class ElectionInvitationRepository : IElectionInvitationRepository
    {
        private readonly ElectionDbContext _db;

        public ElectionInvitationRepository(ElectionDbContext db)
        {
            _db = db;
        }

        public Task<List<ElectionInvitation>> GetByElectionAsync(Guid electionId)
            => _db.ElectionInvitations
                .Where(i => i.ElectionId == electionId)
                .OrderBy(i => i.Email)
                .ToListAsync();

        public Task<ElectionInvitation?> GetByIdAsync(Guid invitationId)
            => _db.ElectionInvitations.FirstOrDefaultAsync(i => i.Id == invitationId);

        public Task<List<string>> GetExistingEmailsAsync(Guid electionId, IEnumerable<string> emails)
        {
            var emailList = emails.ToList();
            return _db.ElectionInvitations
                .Where(i => i.ElectionId == electionId && emailList.Contains(i.Email))
                .Select(i => i.Email)
                .ToListAsync();
        }

        public async Task AddRangeAsync(IEnumerable<ElectionInvitation> invitations)
            => await _db.ElectionInvitations.AddRangeAsync(invitations);

        public void Remove(ElectionInvitation invitation)
            => _db.ElectionInvitations.Remove(invitation);

        public Task SaveChangesAsync()
            => _db.SaveChangesAsync();
    }
}
