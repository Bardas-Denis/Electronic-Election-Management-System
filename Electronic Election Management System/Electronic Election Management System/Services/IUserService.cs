using Electronic_Election_Management_System.DTOs;

namespace Electronic_Election_Management_System.Services
{
    public interface IUserService
    {
        Task<List<UserDto>> GetAllAsync();
        Task<ServiceResult<UserDto>> UpdateRoleAsync(Guid targetId, UpdateUserRoleRequest request, Guid currentUserId);
        Task<ServiceResult<bool>> DeleteAsync(Guid targetId, Guid currentUserId);
    }
}
