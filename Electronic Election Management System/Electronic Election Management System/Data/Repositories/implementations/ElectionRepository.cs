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
                .Include(e => e.Questions).ThenInclude(q => q.Options)
                .OrderByDescending(e => e.CreatedAt)
                .ToListAsync();

        public async Task<List<Election>> GetVisibleToUserAsync(Guid userId)
        {
            var email = await _db.Users
                .Where(u => u.Id == userId)
                .Select(u => u.Email)
                .FirstOrDefaultAsync();

            if (email is null)
                return new List<Election>();

            return await _db.Elections
                .Where(e => !e.IsClosed ||
                            e.CreatedByUserId == userId ||
                            e.Invitations.Any(i => i.UserId == userId || i.Email == email))
                .Include(e => e.Options)
                .Include(e => e.Questions).ThenInclude(q => q.Options)
                .OrderByDescending(e => e.CreatedAt)
                .ToListAsync();
        }

        public Task<List<Election>> GetByCreatedByAsync(Guid userId)
            => _db.Elections
                .Where(e => e.CreatedByUserId == userId)
                .Include(e => e.Options)
                .Include(e => e.Questions).ThenInclude(q => q.Options)
                .OrderByDescending(e => e.CreatedAt)
                .ToListAsync();

        public Task<Election?> GetByIdWithOptionsAsync(Guid id)
            => _db.Elections
                .Include(e => e.Options)
                .Include(e => e.Questions).ThenInclude(q => q.Options)
                .FirstOrDefaultAsync(e => e.Id == id);

        public async Task<Election?> GetAccessibleByIdWithOptionsAsync(Guid id, Guid userId)
        {
            var email = await _db.Users
                .Where(u => u.Id == userId)
                .Select(u => u.Email)
                .FirstOrDefaultAsync();

            if (email is null)
                return null;

            return await _db.Elections
                .Where(e => e.Id == id &&
                            (!e.IsClosed ||
                             e.CreatedByUserId == userId ||
                             e.Invitations.Any(i => i.UserId == userId || i.Email == email)))
                .Include(e => e.Options)
                .Include(e => e.Questions).ThenInclude(q => q.Options)
                .FirstOrDefaultAsync();
        }

        public Task<Election?> GetByIdAsync(Guid id)
            => _db.Elections.FirstOrDefaultAsync(e => e.Id == id);

        public Task<Election?> GetByIdWithResultsAsync(Guid id)
            => _db.Elections
                .Include(e => e.Options)
                    .ThenInclude(o => o.Votes)
                .Include(e => e.Questions).ThenInclude(q => q.Options).ThenInclude(o => o.Votes)
                .FirstOrDefaultAsync(e => e.Id == id);

        public async Task<bool> CanUserAccessAsync(Guid electionId, Guid userId)
        {
            var email = await _db.Users
                .Where(u => u.Id == userId)
                .Select(u => u.Email)
                .FirstOrDefaultAsync();

            return email is not null && await _db.Elections.AnyAsync(e =>
                e.Id == electionId &&
                (!e.IsClosed ||
                 e.CreatedByUserId == userId ||
                 e.Invitations.Any(i => i.UserId == userId || i.Email == email)));
        }

        public async Task AddAsync(Election election)
            => await _db.Elections.AddAsync(election);

        public void RemoveOptions(IEnumerable<Option> options)
            => _db.Options.RemoveRange(options);

        public void RemoveQuestions(IEnumerable<ElectionQuestion> questions)
            => _db.ElectionQuestions.RemoveRange(questions);

        public void Remove(Election election)
            => _db.Elections.Remove(election);

        public Task SaveChangesAsync()
            => _db.SaveChangesAsync();
    }
}
