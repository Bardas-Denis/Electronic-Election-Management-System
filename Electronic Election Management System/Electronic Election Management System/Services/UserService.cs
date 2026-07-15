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

            // Safety rule: no one can demote the last remaining admin account,
            // regardless of whether the target is the caller themselves or another user.
            if (user.Role == UserRole.Admin && newRole != UserRole.Admin)
            {
                int adminCount = await _users.AdminCountAsync();
                if (adminCount <= 1)
                    return ServiceResult<UserDto>.Fail(
                        "Cannot remove the Admin role from the last remaining admin account.");
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

            // Safety rule: do not allow deleting the last remaining admin account.
            if (user.Role == UserRole.Admin)
            {
                int adminCount = await _users.AdminCountAsync();
                if (adminCount <= 1)
                    return ServiceResult<bool>.Fail(
                        "Cannot delete the last remaining admin account.");
            }

            await _auditLogs.AddAsync(new AuditLog
            {
                UserId = currentUserId,
                Action = $"a_sters_utilizatorul:{user.Email}"
            });

            if (await _users.HasCreatedElectionsAsync(targetId))
                return ServiceResult<bool>.Fail(
                    "This user has created at least one election and cannot be deleted. " +
                    "Change their role to Voter instead of deleting them.");

            if (await _users.HasCastNonAnonymousVoteAsync(targetId))
                return ServiceResult<bool>.Fail(
                    "This user has cast a vote in a non-anonymous election and cannot be deleted. " +
                    "Change their role to Voter instead of deleting them.");

            if (await _users.HasCastAnonymousVoteAsync(targetId))
                return ServiceResult<bool>.Fail(
                    "This user has cast a vote (their vote token was consumed) and cannot be deleted. " +
                    "Change their role to Voter instead of deleting them.");

            _users.Remove(user);

            try
            {
                await _users.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // Generic safety net just in case some other constraint is violated.
                return ServiceResult<bool>.Fail(
                   "This user cannot be deleted because other records still reference them.");
            }

            return ServiceResult<bool>.Ok(true);
        }
    }
}
