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

        public Task<List<Label>> GetAllAsync()
            => _db.Labels.OrderBy(l => l.Name).ToListAsync();

        public Task<Label?> GetByIdAsync(Guid id)
            => _db.Labels.FirstOrDefaultAsync(l => l.Id == id);

        public Task<List<Label>> GetByIdsAsync(IEnumerable<Guid> ids)
        {
            var idList = ids.Distinct().ToList();
            return _db.Labels.Where(l => idList.Contains(l.Id)).ToListAsync();
        }

        public Task<bool> ExistsByNameAsync(string name)
            => _db.Labels.AnyAsync(l => l.Name.ToLower() == name.ToLower());

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
