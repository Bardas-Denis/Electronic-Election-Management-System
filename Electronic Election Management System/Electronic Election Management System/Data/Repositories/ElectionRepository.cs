using Electronic_Election_Management_System.Models;
using Microsoft.EntityFrameworkCore;

namespace Electronic_Election_Management_System.Data.Repositories
{
    public class ElectionRepository : IElectionRepository
    {
        private readonly ElectionDbContext _db;

        public ElectionRepository(ElectionDbContext db)
        {
            _db = db;
        }

        public Task<List<Election>> GetAllWithOptionsAsync()
            => _db.Elections
                .Include(e => e.Options)
                .OrderByDescending(e => e.CreatedAt)
                .ToListAsync();

        public Task<Election?> GetByIdWithOptionsAsync(Guid id)
            => _db.Elections
                .Include(e => e.Options)
                .FirstOrDefaultAsync(e => e.Id == id);

        public Task<Election?> GetByIdAsync(Guid id)
            => _db.Elections.FirstOrDefaultAsync(e => e.Id == id);

        public async Task AddAsync(Election election)
            => await _db.Elections.AddAsync(election);

        public void RemoveOptions(IEnumerable<Option> options)
            => _db.Options.RemoveRange(options);

        public void Remove(Election election)
            => _db.Elections.Remove(election);

        public Task SaveChangesAsync()
            => _db.SaveChangesAsync();
    }
}
