using Electronic_Election_Management_System.DTOs;
using Electronic_Election_Management_System.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Electronic_Election_Management_System.Controllers
{
    /// <summary>
    /// Exposes system audit logs for administrators to monitor election-related events
    /// (e.g. vote casts, election creation, role changes, new users).
    /// </summary>
    [ApiController]
    [Route("api/audit")]
    [Authorize(Roles = "Admin")]
    public class AuditController : ControllerBase
    {
        private readonly IAuditService _auditService;

        public AuditController(IAuditService auditService)
        {
            _auditService = auditService;
        }

        /// <summary>
        /// Retrieves the most recent audit log entries, newest first.
        /// </summary>
        /// <param name="take">
        /// Number of entries to return. Clamped server-side to the range 1–500
        /// to prevent accidental full-table pulls.
        /// </param>
        [HttpGet]
        public async Task<ActionResult<List<AuditLogDto>>> GetAll([FromQuery] int take = 100)
        {
            take = Math.Clamp(take, 1, 500);
            var logs = await _auditService.GetLogsAsync(take);
            return Ok(logs);
        }
    }
}
