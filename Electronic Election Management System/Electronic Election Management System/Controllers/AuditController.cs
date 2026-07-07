using Electronic_Election_Management_System.DTOs;
using Electronic_Election_Management_System.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Electronic_Election_Management_System.Controllers
{
    // "vizualizarea auditului" - functionalitate de Administrator ceruta in PDF (sectiunea 5).
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

        // GET /api/audit?take=100
        [HttpGet]
        public async Task<ActionResult<List<AuditLogDto>>> GetAll([FromQuery] int take = 100)
        {
            take = Math.Clamp(take, 1, 500);
            var logs = await _auditService.GetLogsAsync(take);
            return Ok(logs);
        }
    }
}
