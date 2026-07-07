using Electronic_Election_Management_System.Models;

namespace Electronic_Election_Management_System.Data.Repositories
{
    public interface IElectionRepository
    {
        Task<List<Election>> GetAllWithOptionsAsync();
        Task<Election?> GetByIdWithOptionsAsync(Guid id);
        Task<Election?> GetByIdAsync(Guid id);
        Task AddAsync(Election election);
        void RemoveOptions(IEnumerable<Option> options);
        void Remove(Election election);
        Task SaveChangesAsync();
    }
}
