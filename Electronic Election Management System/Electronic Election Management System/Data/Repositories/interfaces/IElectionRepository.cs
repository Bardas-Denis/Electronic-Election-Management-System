using Electronic_Election_Management_System.Models;

namespace Electronic_Election_Management_System.Data.Repositories
{
    public interface IElectionRepository
    {
        /// <summary>Retrieves all elections including their options.</summary>
        Task<List<Election>> GetAllWithOptionsAsync();

        /// <summary>Retrieves public elections and closed elections accessible to a user.</summary>
        Task<List<Election>> GetVisibleToUserAsync(Guid userId);

        /// <summary>Retrieves all elections created by a specific user, including their options.</summary>
        Task<List<Election>> GetByCreatedByAsync(Guid userId);

        /// <summary>Retrieves an election by its ID, including its options.</summary>
        Task<Election?> GetByIdWithOptionsAsync(Guid id);
        Task<Election?> GetAccessibleByIdWithOptionsAsync(Guid id, Guid userId);
        Task<Election?> GetByIdAsync(Guid id);

        /// <summary>Retrieves an election including its options and each option's votes, for results tallying.</summary>
        Task<Election?> GetByIdWithResultsAsync(Guid id);
        Task<bool> CanUserAccessAsync(Guid electionId, Guid userId);

        Task AddAsync(Election election);
        /// <summary>Removes a collection of options.</summary>
        void RemoveOptions(IEnumerable<Option> options);
        void RemoveQuestions(IEnumerable<ElectionQuestion> questions);
        void Remove(Election election);
        Task SaveChangesAsync();
    }
}
