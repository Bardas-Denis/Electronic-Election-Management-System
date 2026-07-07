using Electronic_Election_Management_System.Data;
using Electronic_Election_Management_System.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Electronic_Election_Management_System.Controllers
{
    // "vizualizarea auditului" - functionalitate de Administrator ceruta in PDF (sectiunea 5).
    [ApiController]
    [Route("api/audit")]
    [Authorize(Roles = "Admin")]
    public class AuditController : ControllerBase
    {
        private readonly ElectionDbContext _db;

        public AuditController(ElectionDbContext db)
        {
            _db = db;
        }

        // GET /api/audit?take=100
        [HttpGet]
        public async Task<ActionResult<List<AuditLogDto>>> GetAll([FromQuery] int take = 100)
        {
            take = Math.Clamp(take, 1, 500);

            var logs = await _db.AuditLogs
                .Include(a => a.User)
                .Include(a => a.Election)
                .OrderByDescending(a => a.Timestamp)
                .Take(take)
                .Select(a => new AuditLogDto
                {
                    Id = a.Id,
                    UserEmail = a.User != null ? a.User.Email : "necunoscut",
                    ElectionTitle = a.Election != null ? a.Election.Title : null,
                    Action = a.Action,
                    Timestamp = a.Timestamp
                })
                .ToListAsync();

            return Ok(logs);
        }
    }
}
