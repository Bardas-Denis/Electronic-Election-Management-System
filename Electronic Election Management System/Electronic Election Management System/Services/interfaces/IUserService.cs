using Electronic_Election_Management_System.DTOs;

namespace Electronic_Election_Management_System.Services
{
    public interface IUserService
    {
        /// <summary>
        /// Retrieves all users.
        /// </summary>
        Task<List<UserDto>> GetAllAsync();

        /// <summary>
        /// Updates the role of a user.
        /// </summary>
        /// <param name="targetId">The ID of the user to update.</param>
        /// <param name="request">The role update request.</param>
        /// <param name="currentUserId">The ID of the current user (admin) performing the update.</param>
        Task<ServiceResult<UserDto>> UpdateRoleAsync(Guid targetId, UpdateUserRoleRequest request, Guid currentUserId);

        /// <summary>
        /// Deletes a user.
        /// </summary>
        /// <param name="targetId">The ID of the user to delete.</param>
        /// <param name="currentUserId">The ID of the current user (admin) performing the delete.</param>
        Task<ServiceResult<bool>> DeleteAsync(Guid targetId, Guid currentUserId);
    }
}
