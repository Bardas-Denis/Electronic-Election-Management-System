using Electronic_Election_Management_System.Models;

namespace Electronic_Election_Management_System.Data.Repositories
{
    public interface IElectionRepository
    {
        /// <summary>Retrieves all elections including their options.</summary>
        Task<List<Election>> GetAllWithOptionsAsync();

        /// <summary>Retrieves an election by its ID, including its options.</summary>
        Task<Election?> GetByIdWithOptionsAsync(Guid id);
        Task<Election?> GetByIdAsync(Guid id);
        Task AddAsync(Election election);
        /// <summary>Removes a collection of options.</summary>
        void RemoveOptions(IEnumerable<Option> options);
        void Remove(Election election);
        Task SaveChangesAsync();
    }
}
