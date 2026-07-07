using System.Security.Claims;
using Electronic_Election_Management_System.Data;
using Electronic_Election_Management_System.DTOs;
using Electronic_Election_Management_System.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Electronic_Election_Management_System.Controllers
{
    // Ruta de baza corespunde VotingService din frontend: `${environment.apiUrl}/voting`
    [ApiController]
    [Route("api/voting/elections")]
    [Authorize] // orice utilizator autentificat poate vedea alegerile
    public class ElectionsController : ControllerBase
    {
        private readonly ElectionDbContext _db;

        public ElectionsController(ElectionDbContext db)
        {
            _db = db;
        }

        // GET /api/voting/elections
        [HttpGet]
        public async Task<ActionResult<List<ElectionDto>>> GetAll()
        {
            var elections = await _db.Elections
                .Include(e => e.Options)
                .OrderByDescending(e => e.CreatedAt)
                .ToListAsync();

            return Ok(elections.Select(MapToDto).ToList());
        }

        // GET /api/voting/elections/{id}
        [HttpGet("{id:guid}")]
        public async Task<ActionResult<ElectionDto>> GetById(Guid id)
        {
            var election = await _db.Elections
                .Include(e => e.Options)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (election is null)
            {
                return NotFound();
            }

            return Ok(MapToDto(election));
        }

        // POST /api/voting/elections  (doar Admin - CRUD alegeri, Etapa 2)
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ElectionDto>> Create(CreateElectionRequest request)
        {
            if (!TryParseType(request.Type, out var type))
            {
                return BadRequest(new { message = "Tip invalid. Valorile acceptate sunt 'Politic' sau 'Comercial'." });
            }

            if (request.OptionLabels.Count(l => !string.IsNullOrWhiteSpace(l)) < 2)
            {
                return BadRequest(new { message = "O alegere trebuie sa aiba cel putin 2 optiuni de vot." });
            }

            if (request.EndsAt <= request.StartsAt)
            {
                return BadRequest(new { message = "Data de sfarsit trebuie sa fie dupa data de inceput." });
            }

            var election = new Election
            {
                CreatedByUserId = GetCurrentUserId(),
                Title = request.Title.Trim(),
                Description = request.Description,
                Type = type,
                IsAnonymous = request.IsAnonymous,
                StartsAt = request.StartsAt,
                EndsAt = request.EndsAt,
                Options = request.OptionLabels
                    .Where(l => !string.IsNullOrWhiteSpace(l))
                    .Select(l => new Option { Label = l.Trim() })
                    .ToList()
            };

            _db.Elections.Add(election);

            _db.AuditLogs.Add(new AuditLog
            {
                UserId = GetCurrentUserId(),
                ElectionId = election.Id,
                Action = "a_creat_alegere"
            });

            await _db.SaveChangesAsync();

            return CreatedAtAction(nameof(GetById), new { id = election.Id }, MapToDto(election));
        }

        // PUT /api/voting/elections/{id}  (doar Admin)
        [HttpPut("{id:guid}")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ElectionDto>> Update(Guid id, UpdateElectionRequest request)
        {
            if (!TryParseType(request.Type, out var type))
            {
                return BadRequest(new { message = "Tip invalid. Valorile acceptate sunt 'Politic' sau 'Comercial'." });
            }

            var validLabels = request.OptionLabels.Where(l => !string.IsNullOrWhiteSpace(l)).ToList();
            if (validLabels.Count < 2)
            {
                return BadRequest(new { message = "O alegere trebuie sa aiba cel putin 2 optiuni de vot." });
            }

            if (request.EndsAt <= request.StartsAt)
            {
                return BadRequest(new { message = "Data de sfarsit trebuie sa fie dupa data de inceput." });
            }

            var election = await _db.Elections
                .Include(e => e.Options)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (election is null)
            {
                return NotFound();
            }

            election.Title = request.Title.Trim();
            election.Description = request.Description;
            election.Type = type;
            election.IsAnonymous = request.IsAnonymous;
            election.StartsAt = request.StartsAt;
            election.EndsAt = request.EndsAt;

            // Inlocuim optiunile existente cu cele noi.
            _db.Options.RemoveRange(election.Options);
            election.Options = validLabels.Select(l => new Option { Label = l.Trim(), ElectionId = election.Id }).ToList();

            _db.AuditLogs.Add(new AuditLog
            {
                UserId = GetCurrentUserId(),
                ElectionId = election.Id,
                Action = "a_modificat_alegere"
            });

            await _db.SaveChangesAsync();

            return Ok(MapToDto(election));
        }

        // DELETE /api/voting/elections/{id}  (doar Admin)
        [HttpDelete("{id:guid}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var election = await _db.Elections.FindAsync(id);
            if (election is null)
            {
                return NotFound();
            }

            _db.Elections.Remove(election);

            _db.AuditLogs.Add(new AuditLog
            {
                UserId = GetCurrentUserId(),
                ElectionId = null, // alegerea urmeaza sa fie stearsa, nu mai pastram FK
                Action = $"a_sters_alegere:{election.Title}"
            });

            await _db.SaveChangesAsync();

            return NoContent();
        }

        private static bool TryParseType(string raw, out ElectionType type)
        {
            return Enum.TryParse(raw, ignoreCase: true, out type);
        }

        private static ElectionDto MapToDto(Election e) => new()
        {
            Id = e.Id,
            Title = e.Title,
            Description = e.Description,
            Type = e.Type.ToString(),
            IsAnonymous = e.IsAnonymous,
            StartsAt = e.StartsAt,
            EndsAt = e.EndsAt,
            Options = e.Options.Select(o => new OptionDto { Id = o.Id, Label = o.Label }).ToList(),
            HasUserVoted = false // se implementeaza in Etapa 3, odata cu fluxul de votare
        };

        private Guid GetCurrentUserId()
        {
            string? idClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.Parse(idClaim!);
        }
    }
}
