using Electronic_Election_Management_System.DTOs;

namespace Electronic_Election_Management_System.Services
{
    public interface IAuditService
    {
        /// <summary>
        /// Retrieves the most recent audit log entries, newest first.
        /// </summary>
        /// <param name="take">Number of entries to return.
        /// Clamped server-side to the range 1–500 to prevent accidental full-table pulls.</param>
        Task<List<AuditLogDto>> GetLogsAsync(int take);
    }
}
