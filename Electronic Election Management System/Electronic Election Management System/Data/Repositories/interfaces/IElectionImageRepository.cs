using Electronic_Election_Management_System.Models;

namespace Electronic_Election_Management_System.Data.Repositories
{
    /// <summary>Ownership and claim state, without the bytes.</summary>
    public record ImageClaimInfo(Guid Id, Guid? ElectionId, Guid UploadedByUserId);

    /// <summary>What a read needs before it is allowed to touch <see cref="ElectionImage.Content"/>.</summary>
    public record ImageAccessInfo(Guid? ElectionId, Guid UploadedByUserId, string ContentType, string Sha256);

    public interface IElectionImageRepository
    {
        Task<ImageAccessInfo?> GetAccessInfoAsync(Guid id);

        /// <summary>Call only once the caller has been authorized.</summary>
        Task<byte[]?> GetContentAsync(Guid id);

        /// <summary>Missing ids are simply absent from the result.</summary>
        Task<List<ImageClaimInfo>> GetClaimInfoAsync(IReadOnlyCollection<Guid> ids);

        Task AddAsync(ElectionImage image);

        /// <summary>
        /// Attaches images to an election, skipping rows a different election claimed in the
        /// meantime. The returned count falling short of the ids asked for is how that race
        /// surfaces to the caller.
        /// </summary>
        Task<int> ClaimAsync(IReadOnlyCollection<Guid> ids, Guid electionId);

        Task<int> DeleteByElectionExceptAsync(Guid electionId, IReadOnlyCollection<Guid> keepIds);

        Task<int> DeleteUnclaimedOlderThanAsync(DateTime cutoff);

        Task SaveChangesAsync();
    }
}
