using Electronic_Election_Management_System.DTOs;

namespace Electronic_Election_Management_System.Services
{
    public interface IElectionService
    {
        /// <summary>
        /// Retrieves all elections.
        /// </summary>
        Task<List<ElectionDto>> GetAllAsync();

        /// <summary>
        /// Retrieves an election by its ID, or null if not found.
        /// </summary>
        Task<ElectionDto?> GetByIdAsync(Guid id);

        /// <summary>
        /// Creates a new election. Requires at least 2 non-empty options and EndAt to be after StartAt.
        /// </summary>
        Task<ServiceResult<ElectionDto>> CreateAsync(CreateElectionRequest request, Guid userId);

        /// <summary>
        /// Updates an election. Same validations rules as create.
        /// </summary>
        Task<ServiceResult<ElectionDto>> UpdateAsync(Guid id, UpdateElectionRequest request, Guid userId);

        /// <summary>
        /// Deletes an election.
        /// </summary>
        Task<ServiceResult<bool>> DeleteAsync(Guid id, Guid userId);
    }
}
