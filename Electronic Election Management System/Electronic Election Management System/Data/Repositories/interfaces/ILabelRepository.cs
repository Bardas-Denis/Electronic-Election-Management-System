using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Electronic_Election_Management_System.Models;

namespace Electronic_Election_Management_System.Data.Repositories
{
    public interface ILabelRepository
    {
        // --- Label CRUD ---

        /// <summary>
        /// Labels an administrator can hand out by name — employers, interests, departments.
        /// Excludes the geographic tree, which is thousands of nodes and is browsed level by
        /// level rather than listed flat. See <c>LabelCategories</c>.
        /// </summary>
        Task<List<Label>> GetAssignableAsync();

        /// <summary>True when other labels point at this one as their parent.</summary>
        Task<bool> HasChildrenAsync(Guid id);

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
