using Electronic_Election_Management_System.Data;
using Electronic_Election_Management_System.DTOs;
using Electronic_Election_Management_System.Models;
using Electronic_Election_Management_System.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Electronic_Election_Management_System.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly ElectionDbContext _db;
        private readonly ITokenService _tokenService;

        public AuthController(ElectionDbContext db, ITokenService tokenService)
        {
            _db = db;
            _tokenService = tokenService;
        }

        // POST /api/auth/register
        // Orice cont nou creat prin acest endpoint primeste rolul Voter.
        // Un Admin poate promova ulterior un utilizator prin UsersController.
        [HttpPost("register")]
        public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request)
        {
            string normalizedEmail = request.Email.Trim().ToLowerInvariant();

            bool emailExists = await _db.Users.AnyAsync(u => u.Email == normalizedEmail);
            if (emailExists)
            {
                return Conflict(new { message = "Exista deja un cont cu acest email." });
            }

            var user = new User
            {
                Email = normalizedEmail,
                PasswordHash = PasswordHasher.Hash(request.Password),
                Role = UserRole.Voter
            };

            _db.Users.Add(user);

            _db.AuditLogs.Add(new AuditLog
            {
                UserId = user.Id,
                Action = "inregistrare_cont"
            });

            await _db.SaveChangesAsync();

            return Ok(BuildAuthResponse(user));
        }

        // POST /api/auth/login
        [HttpPost("login")]
        public async Task<ActionResult<AuthResponse>> Login(LoginRequest request)
        {
            string normalizedEmail = request.Email.Trim().ToLowerInvariant();

            var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == normalizedEmail);
            if (user is null || !PasswordHasher.Verify(request.Password, user.PasswordHash))
            {
                return Unauthorized(new { message = "Email sau parola incorecte." });
            }

            _db.AuditLogs.Add(new AuditLog
            {
                UserId = user.Id,
                Action = "login"
            });
            await _db.SaveChangesAsync();

            return Ok(BuildAuthResponse(user));
        }

        private AuthResponse BuildAuthResponse(User user)
        {
            var (token, expiresAt) = _tokenService.GenerateToken(user);

            return new AuthResponse
            {
                Token = token,
                UserId = user.Id,
                Email = user.Email,
                Role = user.Role.ToString(),
                ExpiresAt = expiresAt
            };
        }
    }
}
