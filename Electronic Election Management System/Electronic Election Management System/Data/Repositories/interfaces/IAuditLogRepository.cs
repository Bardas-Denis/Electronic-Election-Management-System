using Electronic_Election_Management_System.Models;

namespace Electronic_Election_Management_System.Data.Repositories
{
    public interface IAuditLogRepository
    {
        /// <summary>
        /// Returns the most recent <paramref name="take"/> audit log entries,
        /// with User and Election navigation properties loaded.
        /// </summary>
        Task<List<AuditLog>> GetRecentAsync(int take);

        Task AddAsync(AuditLog log);

        Task SaveChangesAsync();
    }
}
