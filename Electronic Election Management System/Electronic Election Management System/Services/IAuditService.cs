using Electronic_Election_Management_System.DTOs;

namespace Electronic_Election_Management_System.Services
{
    public interface IAuditService
    {
        Task<List<AuditLogDto>> GetLogsAsync(int take);
    }
}
