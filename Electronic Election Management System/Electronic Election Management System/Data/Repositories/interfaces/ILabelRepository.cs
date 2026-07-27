using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Electronic_Election_Management_System.Models;

namespace Electronic_Election_Management_System.Data.Repositories
{
    public interface ILabelRepository
    {
        // --- Label CRUD ---

        /// <summary>Returns all labels ordered by name.</summary>
        Task<List<Label>> GetAllAsync();

        /// <summary>Returns a label by id, or null if not found.</summary>
        Task<Label?> GetByIdAsync(Guid id);

        /// <summary>Returns all labels whose ids are in the given set.</summary>
        Task<List<Label>> GetByIdsAsync(IEnumerable<Guid> ids);

        /// <summary>Returns true if a label with the given name already exists (case-insensitive).</summary>
        Task<bool> ExistsByNameAsync(string name);

        Task AddAsync(Label label);
        void Remove(Label label);

        // --- UserLabel operations ---

        /// <summary>Returns all UserLabel rows for the given user, including the Label navigation.</summary>
        Task<List<UserLabel>> GetUserLabelsAsync(Guid userId);

        /// <summary>Returns all UserLabel rows for the given label, including the User navigation.</summary>
        Task<List<UserLabel>> GetUsersWithLabelAsync(Guid labelId);

        /// <summary>
        /// Assigns the given labels to the user, skipping any that are already assigned.
        /// Returns the full list of UserLabel rows for the user after the operation.
        /// </summary>
        Task<List<UserLabel>> AssignLabelsAsync(Guid userId, IEnumerable<Guid> labelIds, Guid adminId);

        /// <summary>Removes a specific label from a user. Returns false if the assignment did not exist.</summary>
        Task<bool> RemoveUserLabelAsync(Guid userId, Guid labelId);

        Task SaveChangesAsync();
    }
}
