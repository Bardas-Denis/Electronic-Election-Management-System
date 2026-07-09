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
        Task AddAsync(User user);
        void Remove(User user);
        Task SaveChangesAsync();
    }
}
