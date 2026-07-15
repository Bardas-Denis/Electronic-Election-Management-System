using Electronic_Election_Management_System.Models;

namespace Electronic_Election_Management_System.Data.Repositories
{
    public interface IUserRepository
    {
        Task<List<User>> GetAllAsync();
        Task<User?> GetByIdAsync(Guid id);
        Task<User?> GetByEmailAsync(string normalizedEmail);
        /// <summary>Verifies if an user with the given email exists.</summary>
        Task<bool> ExistsByEmailAsync(string normalizedEmail);
        /// <summary>Returns the count of admin users.</summary>
        Task<int> AdminCountAsync();
        /// <summary>Checks whether the user has created at least one election.</summary>
        Task<bool> HasCreatedElectionsAsync(Guid userId);
        /// <summary>Checks whether the user has cast a vote in a non-anonymous election.</summary>
        Task<bool> HasCastNonAnonymousVoteAsync(Guid userId);
        /// <summary>Checks whether the user has consumed a vote token (cast an anonymous vote).</summary>
        Task<bool> HasCastAnonymousVoteAsync(Guid userId);
        Task AddAsync(User user);
        void Remove(User user);
        Task SaveChangesAsync();
    }
}
