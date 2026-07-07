using Electronic_Election_Management_System.DTOs;

namespace Electronic_Election_Management_System.Services
{
    public interface IElectionService
    {
        Task<List<ElectionDto>> GetAllAsync();
        Task<ElectionDto?> GetByIdAsync(Guid id);
        Task<ServiceResult<ElectionDto>> CreateAsync(CreateElectionRequest request, Guid userId);
        Task<ServiceResult<ElectionDto>> UpdateAsync(Guid id, UpdateElectionRequest request, Guid userId);
        Task<ServiceResult<bool>> DeleteAsync(Guid id, Guid userId);
    }
}
