using Electronic_Election_Management_System.Data.Repositories;
using Electronic_Election_Management_System.DTOs;
using Electronic_Election_Management_System.Models;
using Microsoft.EntityFrameworkCore;

namespace Electronic_Election_Management_System.Services
{
    public class UserService : IUserService
    {
        private readonly IUserRepository _users;
        private readonly IAuditLogRepository _auditLogs;

        public UserService(IUserRepository users, IAuditLogRepository auditLogs)
        {
            _users = users;
            _auditLogs = auditLogs;
        }

        public async Task<List<UserDto>> GetAllAsync()
        {
            var users = await _users.GetAllAsync();
            return users.Select(u => new UserDto
            {
                Id = u.Id,
                Email = u.Email,
                Role = u.Role.ToString(),
                CreatedAt = u.CreatedAt
            }).ToList();
        }

        public async Task<ServiceResult<UserDto>> UpdateRoleAsync(
            Guid targetId, UpdateUserRoleRequest request, Guid currentUserId)
        {
            if (!Enum.TryParse<UserRole>(request.Role, ignoreCase: true, out var newRole))
                return ServiceResult<UserDto>.Fail("Rol invalid. Valorile acceptate sunt 'Admin' sau 'Voter'.");

            var user = await _users.GetByIdAsync(targetId);
            if (user is null)
                return ServiceResult<UserDto>.NotFound("Utilizatorul nu a fost gasit.");

            // Safety rule: an admin cannot demote the last remaining admin account.
            if (user.Id == currentUserId && newRole != UserRole.Admin)
            {
                int adminCount = await _users.AdminCountAsync();
                if (adminCount <= 1)
                    return ServiceResult<UserDto>.Fail(
                        "Nu poti elimina rolul de Admin al singurului administrator ramas.");
            }

            user.Role = newRole;

            await _auditLogs.AddAsync(new AuditLog
            {
                UserId = currentUserId,
                Action = $"a_schimbat_rolul_utilizatorului:{user.Email}->{newRole}"
            });

            await _users.SaveChangesAsync();

            return ServiceResult<UserDto>.Ok(new UserDto
            {
                Id = user.Id,
                Email = user.Email,
                Role = user.Role.ToString(),
                CreatedAt = user.CreatedAt
            });
        }

        public async Task<ServiceResult<bool>> DeleteAsync(Guid targetId, Guid currentUserId)
        {
            if (targetId == currentUserId)
                return ServiceResult<bool>.Fail(
                    "Nu iti poti sterge propriul cont din panoul de administrare.");

            var user = await _users.GetByIdAsync(targetId);
            if (user is null)
                return ServiceResult<bool>.NotFound("Utilizatorul nu a fost gasit.");

            await _auditLogs.AddAsync(new AuditLog
            {
                UserId = currentUserId,
                Action = $"a_sters_utilizatorul:{user.Email}"
            });

            _users.Remove(user);

            try
            {
                await _users.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // The user has created at least one election (FK constraint on Elections.CreatedByUserId).
                // Note: DbUpdateException is caught here as a known pragmatic tradeoff —
                // the alternative (pre-checking for FK references) would require an extra DB query
                // on every delete, for a constraint that is rarely violated.
                return ServiceResult<bool>.Fail(
                    "Acest utilizator a creat cel putin o alegere si nu poate fi sters. " +
                    "Schimba-i rolul in Voter in loc sa il stergi.");
            }

            return ServiceResult<bool>.Ok(true);
        }
    }
}
