using Electronic_Election_Management_System.Models;
using Microsoft.EntityFrameworkCore;

namespace Electronic_Election_Management_System.Data.Repositories
{
    public class AuditLogRepository : IAuditLogRepository
    {
        private readonly ElectionDbContext _db;

        public AuditLogRepository(ElectionDbContext db)
        {
            _db = db;
        }

        public Task<List<AuditLog>> GetRecentAsync(int take)
            => _db.AuditLogs
                .Include(a => a.User)
                .Include(a => a.Election)
                .OrderByDescending(a => a.Timestamp)
                .Take(take)
                .ToListAsync();

        public async Task AddAsync(AuditLog log)
            => await _db.AuditLogs.AddAsync(log);

        public Task SaveChangesAsync()
            => _db.SaveChangesAsync();
    }
}
