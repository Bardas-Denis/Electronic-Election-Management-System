using Electronic_Election_Management_System.Data.Repositories;
using Electronic_Election_Management_System.DTOs;

namespace Electronic_Election_Management_System.Services
{
    public class AuditService : IAuditService
    {
        private readonly IAuditLogRepository _auditLogs;

        public AuditService(IAuditLogRepository auditLogs)
        {
            _auditLogs = auditLogs;
        }

        public async Task<List<AuditLogDto>> GetLogsAsync(int take)
        {
            var logs = await _auditLogs.GetRecentAsync(take);

            return logs.Select(a => new AuditLogDto
            {
                Id = a.Id,
                UserEmail = a.User != null ? a.User.Email : "Unknown",
                ElectionTitle = a.Election?.Title,
                Action = a.Action,
                Timestamp = a.Timestamp
            }).ToList();
        }
    }
}
