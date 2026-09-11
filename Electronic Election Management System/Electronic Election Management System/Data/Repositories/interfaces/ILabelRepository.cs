using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Electronic_Election_Management_System.Models;

namespace Electronic_Election_Management_System.Data.Repositories
{
    /// <summary>
    /// One node of the geographic tree, flattened for browsing. <paramref name="HasChildren"/>
    /// is computed in the same query so a caller never has to ask node by node.
    /// </summary>
    public record GeographicNode(
        Guid Id, string Name, string? Code, string? Category, string? Kind, bool HasChildren);

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

        // --- Geographic tree ---

        /// <summary>Every country, ordered by name. Roughly 240 rows.</summary>
        Task<List<GeographicNode>> GetCountriesAsync();

        /// <summary>
        /// The direct children of one node, ordered by name. Never the whole subtree — the tree
        /// is browsed one level at a time so no request ever carries thousands of rows.
        /// </summary>
        Task<List<GeographicNode>> GetChildrenAsync(Guid parentId);

        /// <summary>
        /// True when a sibling under <paramref name="parentId"/> already carries this name,
        /// ignoring <paramref name="excludeId"/> so a rename can keep its own name.
        /// Mirrors the unique index on (ParentId, Name), turning a database failure into an error.
        /// </summary>
        Task<bool> NameTakenUnderParentAsync(Guid? parentId, string name, Guid? excludeId);

        /// <summary>
        /// True when <paramref name="candidateId"/> sits anywhere below <paramref name="nodeId"/>.
        /// Walking up from the candidate is what stops a move from closing a loop.
        /// </summary>
        Task<bool> IsDescendantOfAsync(Guid nodeId, Guid candidateId);

        /// <summary>How many users carry this label.</summary>
        Task<int> CountUsersWithLabelAsync(Guid labelId);

        /// <summary>
        /// Moves every user from one label to another and returns how many were touched. A user
        /// who already carries the destination simply loses the source, because (UserId, LabelId)
        /// is the primary key and a second copy would fail the insert.
        /// </summary>
        Task<int> ReassignUserLabelsAsync(Guid fromLabelId, Guid toLabelId);

        /// <summary>Returns a label by id, or null if not found.</summary>
        Task<Label?> GetByIdAsync(Guid id);

        /// <summary>
        /// The geographic node carrying this ISO code, or null. Codes are unique among the labels
        /// that have one, so there is never more than a single match.
        /// </summary>
        Task<Label?> GetByCodeAsync(string code);

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
