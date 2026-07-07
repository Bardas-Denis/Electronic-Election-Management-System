using System.Security.Claims;
using ElectionSystem.Api.Data;
using ElectionSystem.Api.Dtos;
using ElectionSystem.Api.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ElectionSystem.Api.Controllers
{
    // Toate endpoint-urile de aici sunt exclusiv pentru Administrator (gestionarea utilizatorilor si rolurilor - Etapa 2).
    [ApiController]
    [Route("api/users")]
    [Authorize(Roles = "Admin")]
    public class UsersController : ControllerBase
    {
        private readonly ElectionDbContext _db;

        public UsersController(ElectionDbContext db)
        {
            _db = db;
        }

        // GET /api/users
        [HttpGet]
        public async Task<ActionResult<List<UserDto>>> GetAll()
        {
            var users = await _db.Users
                .OrderBy(u => u.Email)
                .Select(u => new UserDto
                {
                    Id = u.Id,
                    Email = u.Email,
                    Role = u.Role.ToString(),
                    CreatedAt = u.CreatedAt
                })
                .ToListAsync();

            return Ok(users);
        }

        // PUT /api/users/{id}/role
        [HttpPut("{id:guid}/role")]
        public async Task<ActionResult<UserDto>> UpdateRole(Guid id, UpdateUserRoleRequest request)
        {
            if (!Enum.TryParse<UserRole>(request.Role, ignoreCase: true, out var newRole))
            {
                return BadRequest(new { message = "Rol invalid. Valorile acceptate sunt 'Admin' sau 'Voter'." });
            }

            var user = await _db.Users.FindAsync(id);
            if (user is null)
            {
                return NotFound();
            }

            // Un admin nu isi poate retrograda singur ultimul cont de Admin ramas,
            // ca sa nu se blocheze accesul la panoul de administrare.
            var currentUserId = GetCurrentUserId();
            if (user.Id == currentUserId && newRole != UserRole.Admin)
            {
                bool isLastAdmin = await _db.Users.CountAsync(u => u.Role == UserRole.Admin) <= 1;
                if (isLastAdmin)
                {
                    return BadRequest(new { message = "Nu poti elimina rolul de Admin al singurului administrator ramas." });
                }
            }

            user.Role = newRole;

            _db.AuditLogs.Add(new AuditLog
            {
                UserId = currentUserId,
                Action = $"a_schimbat_rolul_utilizatorului:{user.Email}->{newRole}"
            });

            await _db.SaveChangesAsync();

            return Ok(new UserDto
            {
                Id = user.Id,
                Email = user.Email,
                Role = user.Role.ToString(),
                CreatedAt = user.CreatedAt
            });
        }

        // DELETE /api/users/{id}
        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var currentUserId = GetCurrentUserId();
            if (id == currentUserId)
            {
                return BadRequest(new { message = "Nu iti poti sterge propriul cont din panoul de administrare." });
            }

            var user = await _db.Users.FindAsync(id);
            if (user is null)
            {
                return NotFound();
            }

            _db.Users.Remove(user);

            _db.AuditLogs.Add(new AuditLog
            {
                UserId = currentUserId,
                Action = $"a_sters_utilizatorul:{user.Email}"
            });

            try
            {
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                return Conflict(new
                {
                    message = "Acest utilizator a creat cel putin o alegere si nu poate fi sters. Schimba-i rolul in Voter in loc sa il stergi."
                });
            }

            return NoContent();
        }

        private Guid GetCurrentUserId()
        {
            string? idClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.Parse(idClaim!);
        }
    }
}
