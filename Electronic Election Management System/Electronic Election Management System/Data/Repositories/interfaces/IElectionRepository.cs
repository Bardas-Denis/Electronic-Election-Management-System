using Electronic_Election_Management_System.Models;
using Microsoft.EntityFrameworkCore.Storage;

namespace Electronic_Election_Management_System.Data.Repositories
{
    public interface IElectionRepository
    {
        /// <summary>Spans several repositories via the shared context, so writing an election
        /// and claiming its images commit as a unit.</summary>
        Task<IDbContextTransaction> BeginTransactionAsync();

        Task<List<Election>> GetAllWithOptionsAsync();

        Task<List<Election>> GetVisibleToUserAsync(Guid userId);

        Task<List<Election>> GetByCreatedByAsync(Guid userId);

        Task<Election?> GetByIdWithOptionsAsync(Guid id);
        Task<Election?> GetAccessibleByIdWithOptionsAsync(Guid id, Guid userId);
        Task<Election?> GetByIdAsync(Guid id);

        /// <summary>Includes each option's votes, for tallying.</summary>
        Task<Election?> GetByIdWithResultsAsync(Guid id);
        Task<bool> CanUserAccessAsync(Guid electionId, Guid userId);

        Task AddAsync(Election election);
        Task AddQuestionsAsync(IEnumerable<ElectionQuestion> questions);
        void RemoveOptions(IEnumerable<Option> options);
        void RemoveQuestions(IEnumerable<ElectionQuestion> questions);
        void Remove(Election election);
        Task SaveChangesAsync();
    }
}
