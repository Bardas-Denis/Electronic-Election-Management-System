using Electronic_Election_Management_System.Models;
using Microsoft.EntityFrameworkCore;

namespace Electronic_Election_Management_System.Data.Repositories
{
    public class UserRepository : IUserRepository
    {
        private readonly ElectionDbContext _db;

        public UserRepository(ElectionDbContext db)
        {
            _db = db;
        }

        public Task<List<User>> GetAllAsync()
            => _db.Users.OrderBy(u => u.Email).ToListAsync();

        public Task<User?> GetByIdAsync(Guid id)
            => _db.Users.FirstOrDefaultAsync(u => u.Id == id);

        public Task<User?> GetByEmailAsync(string normalizedEmail)
            => _db.Users.FirstOrDefaultAsync(u => u.Email == normalizedEmail);

        public Task<List<User>> GetByIdsAsync(IEnumerable<Guid> ids)
        {
            var idList = ids.Distinct().ToList();
            return _db.Users.Where(u => idList.Contains(u.Id)).ToListAsync();
        }

        public Task<List<User>> GetByEmailsAsync(IEnumerable<string> normalizedEmails)
        {
            var emailList = normalizedEmails.Distinct().ToList();
            return _db.Users.Where(u => emailList.Contains(u.Email)).ToListAsync();
        }

        public Task<bool> ExistsByEmailAsync(string normalizedEmail)
            => _db.Users.AnyAsync(u => u.Email == normalizedEmail);

        public Task<int> AdminCountAsync()
            => _db.Users.CountAsync(u => u.Role == UserRole.Admin);

        public Task<bool> HasCreatedElectionsAsync(Guid userId)
            => _db.Elections.AnyAsync(e => e.CreatedByUserId == userId);

        public Task<bool> HasCastNonAnonymousVoteAsync(Guid userId)
            => _db.Votes.AnyAsync(v => v.UserId == userId);

        public Task<bool> HasCastAnonymousVoteAsync(Guid userId)
            => _db.VoteTokens.AnyAsync(vt => vt.UserId == userId && vt.Vote != null);

        public async Task AddAsync(User user)
            => await _db.Users.AddAsync(user);

        public void Remove(User user)
            => _db.Users.Remove(user);

        public Task SaveChangesAsync()
            => _db.SaveChangesAsync();
    }
}
