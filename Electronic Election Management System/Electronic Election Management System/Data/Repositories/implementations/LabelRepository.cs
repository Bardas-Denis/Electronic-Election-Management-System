using Electronic_Election_Management_System.Constants;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Electronic_Election_Management_System.Models;
using Microsoft.EntityFrameworkCore;

namespace Electronic_Election_Management_System.Data.Repositories
{
    public class LabelRepository : ILabelRepository
    {
        private readonly ElectionDbContext _db;

        public LabelRepository(ElectionDbContext db)
        {
            _db = db;
        }

        public Task<List<Label>> GetAssignableAsync()
            => _db.Labels
                .Where(l => l.Category == null || !LabelCategories.Geographic.Contains(l.Category))
                .OrderBy(l => l.Name)
                .ToListAsync();

        public Task<bool> HasChildrenAsync(Guid id)
            => _db.Labels.AnyAsync(l => l.ParentId == id);

        public Task<List<GeographicNode>> GetCountriesAsync()
            => _db.Labels
                .Where(l => l.Category == LabelCategories.Country)
                .OrderBy(l => l.Name)
                .Select(l => new GeographicNode(l.Id, l.Name, l.Code, l.Category, l.Children.Any()))
                .ToListAsync();

        public Task<bool> NameTakenUnderParentAsync(Guid? parentId, string name, Guid? excludeId)
            => _db.Labels.AnyAsync(l =>
                l.ParentId == parentId &&
                l.Name.ToLower() == name.ToLower() &&
                (excludeId == null || l.Id != excludeId));

        public async Task<bool> IsDescendantOfAsync(Guid nodeId, Guid candidateId)
        {
            // Walks up from the candidate. The visited set is not defensive tidiness: if the tree
            // is ever left with a loop, an unguarded walk here would never return.
            var visited = new HashSet<Guid>();
            var currentId = (Guid?)candidateId;

            while (currentId is not null && visited.Add(currentId.Value))
            {
                if (currentId.Value == nodeId)
                    return true;

                currentId = await _db.Labels
                    .Where(l => l.Id == currentId)
                    .Select(l => l.ParentId)
                    .FirstOrDefaultAsync();
            }

            return false;
        }

        public Task<int> CountUsersWithLabelAsync(Guid labelId)
            => _db.UserLabels.CountAsync(ul => ul.LabelId == labelId);

        public async Task<int> ReassignUserLabelsAsync(Guid fromLabelId, Guid toLabelId)
        {
            var links = await _db.UserLabels.Where(ul => ul.LabelId == fromLabelId).ToListAsync();
            if (links.Count == 0)
                return 0;

            var userIds = links.Select(l => l.UserId).ToList();
            var alreadyThere = await _db.UserLabels
                .Where(ul => ul.LabelId == toLabelId && userIds.Contains(ul.UserId))
                .Select(ul => ul.UserId)
                .ToListAsync();
            var skip = alreadyThere.ToHashSet();

            _db.UserLabels.RemoveRange(links);

            // AssignedAt is carried over rather than reset: it records when the user declared
            // where they live, and moving the node up does not change when they said it.
            var moved = links
                .Where(l => !skip.Contains(l.UserId))
                .Select(l => new UserLabel
                {
                    UserId = l.UserId,
                    LabelId = toLabelId,
                    AssignedBy = l.AssignedBy,
                    AssignedAt = l.AssignedAt
                })
                .ToList();

            await _db.UserLabels.AddRangeAsync(moved);
            return links.Count;
        }

        public Task<List<GeographicNode>> GetChildrenAsync(Guid parentId)
            => _db.Labels
                .Where(l => l.ParentId == parentId)
                .OrderBy(l => l.Name)
                .Select(l => new GeographicNode(l.Id, l.Name, l.Code, l.Category, l.Children.Any()))
                .ToListAsync();

        public Task<Label?> GetByIdAsync(Guid id)
            => _db.Labels.FirstOrDefaultAsync(l => l.Id == id);

        public Task<List<Label>> GetByIdsAsync(IEnumerable<Guid> ids)
        {
            var idList = ids.Distinct().ToList();
            return _db.Labels.Where(l => idList.Contains(l.Id)).ToListAsync();
        }

        public Task<bool> ExistsByNameAsync(string name)
            => _db.Labels.AnyAsync(l =>
                (l.Category == null || !LabelCategories.Geographic.Contains(l.Category)) &&
                l.Name.ToLower() == name.ToLower());

        public async Task AddAsync(Label label)
            => await _db.Labels.AddAsync(label);

        public void Remove(Label label)
            => _db.Labels.Remove(label);

        // --- UserLabel operations ---

        public Task<List<UserLabel>> GetUserLabelsAsync(Guid userId)
            => _db.UserLabels
                  .Where(ul => ul.UserId == userId)
                  .Include(ul => ul.Label)
                  .ToListAsync();

        public Task<List<UserLabel>> GetUsersWithLabelAsync(Guid labelId)
            => _db.UserLabels
                  .Where(ul => ul.LabelId == labelId)
                  .Include(ul => ul.User)
                  .ToListAsync();

        public async Task<List<UserLabel>> AssignLabelsAsync(Guid userId, IEnumerable<Guid> labelIds, Guid adminId)
        {
            var now = DateTime.UtcNow;

            // Fetch existing assignments to avoid violating the composite PK
            var existingLabelIds = await _db.UserLabels
                .Where(ul => ul.UserId == userId)
                .Select(ul => ul.LabelId)
                .ToListAsync();

            foreach (var labelId in labelIds.Distinct())
            {
                if (existingLabelIds.Contains(labelId))
                    continue;

                await _db.UserLabels.AddAsync(new UserLabel
                {
                    UserId = userId,
                    LabelId = labelId,
                    AssignedBy = adminId,
                    AssignedAt = now
                });
            }

            await _db.SaveChangesAsync();

            // Return the full updated list for this user
            return await GetUserLabelsAsync(userId);
        }

        public async Task<bool> RemoveUserLabelAsync(Guid userId, Guid labelId)
        {
            var entity = await _db.UserLabels
                .FirstOrDefaultAsync(ul => ul.UserId == userId && ul.LabelId == labelId);

            if (entity is null)
                return false;

            _db.UserLabels.Remove(entity);
            return true;
        }

        public Task SaveChangesAsync()
            => _db.SaveChangesAsync();
    }
}
