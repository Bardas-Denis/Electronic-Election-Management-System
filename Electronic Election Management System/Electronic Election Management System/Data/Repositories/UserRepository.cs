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

        public Task<bool> ExistsByEmailAsync(string normalizedEmail)
            => _db.Users.AnyAsync(u => u.Email == normalizedEmail);

        public Task<int> AdminCountAsync()
            => _db.Users.CountAsync(u => u.Role == UserRole.Admin);

        public async Task AddAsync(User user)
            => await _db.Users.AddAsync(user);

        public void Remove(User user)
            => _db.Users.Remove(user);

        public Task SaveChangesAsync()
            => _db.SaveChangesAsync();
    }
}
