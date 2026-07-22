using Electronic_Election_Management_System.Constants;
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
        private readonly IUserNotifier _notifier;

        public UserService(IUserRepository users, IAuditLogRepository auditLogs, IUserNotifier notifier)
        {
            _users = users;
            _auditLogs = auditLogs;
            _notifier = notifier;
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
                return ServiceResult<UserDto>.Fail(ErrorCode.InvalidRole);

            var user = await _users.GetByIdAsync(targetId);
            if (user is null)
                return ServiceResult<UserDto>.NotFound(ErrorCode.ResourceNotFound);

            // Safety rule: no one can demote the last remaining admin account,
            // regardless of whether the target is the caller themselves or another user.
            if (user.Role == UserRole.Admin && newRole != UserRole.Admin)
            {
                int adminCount = await _users.AdminCountAsync();
                if (adminCount <= 1)
                    return ServiceResult<UserDto>.Fail(ErrorCode.LastAdminRoleProtected);
            }

            user.Role = newRole;
            // Regenerate security stamp to immediately invalidate all JWT tokens issued previously
            user.SecurityStamp = Guid.NewGuid().ToString();

            await _auditLogs.AddAsync(new AuditLog
            {
                UserId = currentUserId,
                Action = $"{AuditAction.ChangedUserRole.ToDbValue()}:{user.Email}->{newRole}"
            });

            await _users.SaveChangesAsync();

            // Best-effort push: notifies the affected user that their role has changed.
            // If the hub connection is absent the call is a no-op — SecurityStamp revocation remains the primary enforcement mechanism.
            await _notifier.NotifyRoleChangedAsync(targetId);

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
                return ServiceResult<bool>.Fail(ErrorCode.CannotDeleteSelf);

            var user = await _users.GetByIdAsync(targetId);
            if (user is null)
                return ServiceResult<bool>.NotFound(ErrorCode.ResourceNotFound);

            // Safety rule: do not allow deleting the last remaining admin account.
            if (user.Role == UserRole.Admin)
            {
                int adminCount = await _users.AdminCountAsync();
                if (adminCount <= 1)
                    return ServiceResult<bool>.Fail(ErrorCode.LastAdminDeleteProtected);
            }

            await _auditLogs.AddAsync(new AuditLog
            {
                UserId = currentUserId,
                Action = $"{AuditAction.DeletedUser.ToDbValue()}:{user.Email}"
            });

            if (await _users.HasCreatedElectionsAsync(targetId))
                return ServiceResult<bool>.Fail(ErrorCode.UserHasCreatedElections);

            if (await _users.HasCastNonAnonymousVoteAsync(targetId))
                return ServiceResult<bool>.Fail(ErrorCode.UserHasNonAnonymousVote);

            if (await _users.HasCastAnonymousVoteAsync(targetId))
                return ServiceResult<bool>.Fail(ErrorCode.UserHasAnonymousVote);

            _users.Remove(user);

            try
            {
                await _users.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // Generic safety net just in case some other constraint is violated.
                return ServiceResult<bool>.Fail(ErrorCode.UserHasDependentRecords);
            }

            return ServiceResult<bool>.Ok(true);
        }
    }
}
