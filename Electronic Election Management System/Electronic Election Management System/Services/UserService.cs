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
                return ServiceResult<UserDto>.Fail("Invalid role. Accepted values are 'Admin', 'ElectionManager' or 'Voter'.");

            var user = await _users.GetByIdAsync(targetId);
            if (user is null)
                return ServiceResult<UserDto>.NotFound("User not found.");

            // Safety rule: an admin cannot demote the last remaining admin account.
            if (user.Id == currentUserId && newRole != UserRole.Admin)
            {
                int adminCount = await _users.AdminCountAsync();
                if (adminCount <= 1)
                    return ServiceResult<UserDto>.Fail(
                        "You cannot remove the Admin role of the last remaining admin.");
            }

            user.Role = newRole;

            await _auditLogs.AddAsync(new AuditLog
            {
                UserId = currentUserId,
                Action = $"changed_user_role:{user.Email}->{newRole}"
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
                    "You cannot delete your own account from the admin panel.");

            var user = await _users.GetByIdAsync(targetId);
            if (user is null)
                return ServiceResult<bool>.NotFound("User not found.");

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
                // FK violation: user has created elections. Caught here instead of
                // pre-checking, to avoid an extra query for a rare case.
                return ServiceResult<bool>.Fail(
                    "This user has created at least one election and cannot be deleted. " +
                    "Change their role to Voter instead of deleting them.");
            }

            return ServiceResult<bool>.Ok(true);
        }
    }
}
