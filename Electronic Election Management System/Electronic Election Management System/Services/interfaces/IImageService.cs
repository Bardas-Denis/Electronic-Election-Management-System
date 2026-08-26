using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Electronic_Election_Management_System.DTOs;

namespace Electronic_Election_Management_System.Services.interfaces
{
    public interface IImageService
    {
        /// <summary>
        /// Decodes, downscales and re-encodes an upload, storing it as an unclaimed draft owned by
        /// <paramref name="userId"/>. The original bytes are never persisted.
        /// </summary>
        Task<ServiceResult<ImageUploadResultDto>> UploadAsync(Stream content, long byteLength, Guid userId);

        /// <summary>
        /// Metadata for an image the caller may see: their own draft, or one belonging to an
        /// election they can access. Denials come back as not-found, so the existence of a closed
        /// election's images is never confirmed to an outsider.
        /// </summary>
        Task<ServiceResult<ImageMetadataDto>> GetMetadataForUserAsync(Guid imageId, Guid userId);

        /// <summary>
        /// Reads the stored bytes without any access check. Call
        /// <see cref="GetMetadataForUserAsync"/> first and only proceed if it succeeded.
        /// </summary>
        Task<byte[]?> GetContentAsync(Guid imageId);

        /// <summary>
        /// Checks that every referenced image exists, belongs to the caller, and is either an
        /// unclaimed draft or already part of <paramref name="electionId"/> - null when creating.
        /// Runs before the election is written so a bad reference fails the save outright.
        /// </summary>
        Task<ServiceResult<bool>> ValidateClaimableAsync(
            IReadOnlyCollection<Guid> imageIds, Guid userId, Guid? electionId);

        /// <summary>
        /// Attaches images to an election, returning false when another election claimed one of
        /// them in the meantime. Must run inside the transaction that writes the election, both
        /// because <see cref="Models.ElectionImage.ElectionId"/> is a foreign key and so a false
        /// result can roll the save back.
        /// </summary>
        Task<bool> ClaimAsync(IReadOnlyCollection<Guid> imageIds, Guid electionId);

        /// <summary>Deletes the election's images that the latest edit no longer references.</summary>
        Task<int> ReleaseUnreferencedAsync(Guid electionId, IReadOnlyCollection<Guid> keepIds);

        /// <summary>Discards drafts abandoned by a creator who never saved the election.</summary>
        Task<int> DeleteUnclaimedDraftsAsync();
    }
}
