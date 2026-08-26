using Electronic_Election_Management_System.Models;
using Microsoft.EntityFrameworkCore;

namespace Electronic_Election_Management_System.Data.Repositories
{
    public class ElectionImageRepository : IElectionImageRepository
    {
        private readonly ElectionDbContext _db;

        public ElectionImageRepository(ElectionDbContext db)
        {
            _db = db;
        }

        // Projected rather than materialised: EF Core cannot lazy-load a scalar column, so
        // loading the entity would drag the image bytes along with it.
        public Task<ImageAccessInfo?> GetAccessInfoAsync(Guid id)
            => _db.ElectionImages
                .Where(i => i.Id == id)
                .Select(i => new ImageAccessInfo(i.ElectionId, i.UploadedByUserId, i.ContentType, i.Sha256))
                .FirstOrDefaultAsync();

        public Task<byte[]?> GetContentAsync(Guid id)
            => _db.ElectionImages
                .Where(i => i.Id == id)
                .Select(i => i.Content)
                .FirstOrDefaultAsync();

        public Task<List<ImageClaimInfo>> GetClaimInfoAsync(IReadOnlyCollection<Guid> ids)
            => _db.ElectionImages
                .Where(i => ids.Contains(i.Id))
                .Select(i => new ImageClaimInfo(i.Id, i.ElectionId, i.UploadedByUserId))
                .ToListAsync();

        public async Task AddAsync(ElectionImage image)
            => await _db.ElectionImages.AddAsync(image);

        // The ElectionId predicate is what makes this safe to run after a separate validation
        // query: a row another election grabbed in between is left alone rather than stolen.
        public Task<int> ClaimAsync(IReadOnlyCollection<Guid> ids, Guid electionId)
            => _db.ElectionImages
                .Where(i => ids.Contains(i.Id) &&
                            (i.ElectionId == null || i.ElectionId == electionId))
                .ExecuteUpdateAsync(setters => setters.SetProperty(i => i.ElectionId, electionId));

        public Task<int> DeleteByElectionExceptAsync(Guid electionId, IReadOnlyCollection<Guid> keepIds)
            => _db.ElectionImages
                .Where(i => i.ElectionId == electionId && !keepIds.Contains(i.Id))
                .ExecuteDeleteAsync();

        public Task<int> DeleteUnclaimedOlderThanAsync(DateTime cutoff)
            => _db.ElectionImages
                .Where(i => i.ElectionId == null && i.CreatedAt < cutoff)
                .ExecuteDeleteAsync();

        public Task SaveChangesAsync()
            => _db.SaveChangesAsync();
    }
}
