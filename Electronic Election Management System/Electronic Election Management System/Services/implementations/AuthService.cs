using Electronic_Election_Management_System.Constants;
using Electronic_Election_Management_System.Data.Repositories;
using Electronic_Election_Management_System.DTOs;
using Electronic_Election_Management_System.Models;

namespace Electronic_Election_Management_System.Services
{
    public class AuthService : IAuthService
    {
        private readonly IUserRepository _users;
        private readonly IAuditLogRepository _auditLogs;
        private readonly ITokenService _tokenService;

        public AuthService(IUserRepository users, IAuditLogRepository auditLogs, ITokenService tokenService)
        {
            _users = users;
            _auditLogs = auditLogs;
            _tokenService = tokenService;
        }

        public async Task<ServiceResult<AuthResponse>> RegisterAsync(RegisterRequest request)
        {
            string normalizedEmail = request.Email.Trim().ToLowerInvariant();

            bool emailExists = await _users.ExistsByEmailAsync(normalizedEmail);
            if (emailExists)
                return ServiceResult<AuthResponse>.Fail(ErrorCode.EmailAlreadyExists);

            var user = new User
            {
                Email = normalizedEmail,
                PasswordHash = PasswordHasher.Hash(request.Password),
                Role = UserRole.Voter
            };

            await _users.AddAsync(user);
            await _auditLogs.AddAsync(new AuditLog { UserId = user.Id, Action = AuditAction.AccountCreated.ToDbValue() });
            await _users.SaveChangesAsync();

            return ServiceResult<AuthResponse>.Ok(BuildAuthResponse(user));
        }

        public async Task<ServiceResult<AuthResponse>> LoginAsync(LoginRequest request)
        {
            string normalizedEmail = request.Email.Trim().ToLowerInvariant();

            var user = await _users.GetByEmailAsync(normalizedEmail);
            if (user is null || !PasswordHasher.Verify(request.Password, user.PasswordHash))
                return ServiceResult<AuthResponse>.Fail(ErrorCode.InvalidCredentials);

            await _auditLogs.AddAsync(new AuditLog { UserId = user.Id, Action = AuditAction.Login.ToDbValue() });
            await _auditLogs.SaveChangesAsync();

            return ServiceResult<AuthResponse>.Ok(BuildAuthResponse(user));
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
