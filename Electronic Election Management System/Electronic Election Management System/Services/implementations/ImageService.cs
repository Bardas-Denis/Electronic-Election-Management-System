using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Electronic_Election_Management_System.Constants;
using Electronic_Election_Management_System.Data.Repositories;
using Electronic_Election_Management_System.DTOs;
using Electronic_Election_Management_System.Models;
using Electronic_Election_Management_System.Services.interfaces;
using SkiaSharp;

namespace Electronic_Election_Management_System.Services.implementations
{
    public class ImageService : IImageService
    {
        private const string OutputContentType = "image/webp";

        // Mitchell trades a little sharpness for far fewer artefacts when shrinking a photo,
        // which is the only direction uploads are ever scaled.
        private static readonly SKSamplingOptions Sampling = new(SKCubicResampler.Mitchell);

        private readonly IElectionImageRepository _images;
        private readonly IElectionRepository _elections;
        private readonly ILogger<ImageService> _logger;

        public ImageService(
            IElectionImageRepository images,
            IElectionRepository elections,
            ILogger<ImageService> logger)
        {
            _images = images;
            _elections = elections;
            _logger = logger;
        }

        public async Task<ServiceResult<ImageUploadResultDto>> UploadAsync(
            Stream content, long byteLength, Guid userId)
        {
            if (byteLength <= 0 || byteLength > ValidationRules.ImageMaxUploadBytes)
                return ServiceResult<ImageUploadResultDto>.Fail(ErrorCode.ImageTooLarge);

            using var buffer = new MemoryStream();
            await content.CopyToAsync(buffer);
            var raw = buffer.ToArray();

            // The declared length is only a hint; this is the size that actually arrived.
            if (raw.Length == 0 || raw.Length > ValidationRules.ImageMaxUploadBytes)
                return ServiceResult<ImageUploadResultDto>.Fail(ErrorCode.ImageTooLarge);

            var processed = TryProcess(raw, out var rejection);
            if (processed is null)
            {
                _logger.LogWarning(
                    "Rejected image upload from UserId {UserId} ({ByteCount} bytes): {Reason}",
                    userId, raw.Length, rejection);
                return ServiceResult<ImageUploadResultDto>.Fail(rejection);
            }

            var image = new ElectionImage
            {
                UploadedByUserId = userId,
                Sha256 = Convert.ToHexStringLower(SHA256.HashData(processed.Content)),
                ContentType = OutputContentType,
                ByteSize = processed.Content.Length,
                Width = processed.Width,
                Height = processed.Height,
                Content = processed.Content
            };

            await _images.AddAsync(image);
            await _images.SaveChangesAsync();

            _logger.LogInformation(
                "Image {ImageId} stored for UserId {UserId}: {InputBytes} bytes in, {OutputBytes} bytes out ({Width}x{Height})",
                image.Id, userId, raw.Length, image.ByteSize, image.Width, image.Height);

            return ServiceResult<ImageUploadResultDto>.Ok(new ImageUploadResultDto
            {
                Id = image.Id,
                Width = image.Width,
                Height = image.Height,
                ByteSize = image.ByteSize,
                ContentType = image.ContentType
            });
        }

        public async Task<ServiceResult<ImageMetadataDto>> GetMetadataForUserAsync(Guid imageId, Guid userId)
        {
            var info = await _images.GetAccessInfoAsync(imageId);
            if (info is null)
                return ServiceResult<ImageMetadataDto>.NotFound();

            var allowed = info.ElectionId is null
                ? info.UploadedByUserId == userId
                : await _elections.CanUserAccessAsync(info.ElectionId.Value, userId);

            if (!allowed)
            {
                // Not-found rather than forbidden: confirming the image exists would leak what a
                // closed election's visibility rules are there to protect.
                _logger.LogWarning("Denied image {ImageId} to UserId {UserId}", imageId, userId);
                return ServiceResult<ImageMetadataDto>.NotFound();
            }

            return ServiceResult<ImageMetadataDto>.Ok(
                new ImageMetadataDto(info.ContentType, info.Sha256));
        }

        public Task<byte[]?> GetContentAsync(Guid imageId)
            => _images.GetContentAsync(imageId);

        public async Task<ServiceResult<bool>> ValidateClaimableAsync(
            IReadOnlyCollection<Guid> imageIds, Guid userId, Guid? electionId)
        {
            var distinctIds = imageIds.Distinct().ToList();
            if (distinctIds.Count == 0)
                return ServiceResult<bool>.Ok(true);

            var claimInfo = await _images.GetClaimInfoAsync(distinctIds);

            // A missing row means the id was invented, or the draft has already been swept.
            if (claimInfo.Count != distinctIds.Count)
                return ServiceResult<bool>.Fail(ErrorCode.InvalidImageReference);

            foreach (var info in claimInfo)
            {
                if (info.UploadedByUserId != userId)
                    return ServiceResult<bool>.Fail(ErrorCode.InvalidImageReference);

                // Either still an unclaimed draft, or already part of the election being edited.
                if (info.ElectionId is not null && info.ElectionId != electionId)
                    return ServiceResult<bool>.Fail(ErrorCode.InvalidImageReference);
            }

            return ServiceResult<bool>.Ok(true);
        }

        public async Task<bool> ClaimAsync(IReadOnlyCollection<Guid> imageIds, Guid electionId)
        {
            var distinctIds = imageIds.Distinct().ToList();
            if (distinctIds.Count == 0)
                return true;

            var claimed = await _images.ClaimAsync(distinctIds, electionId);
            if (claimed == distinctIds.Count)
                return true;

            _logger.LogWarning(
                "Election {ElectionId} claimed only {Claimed} of {Requested} images; another election took the rest.",
                electionId, claimed, distinctIds.Count);
            return false;
        }

        public Task<int> ReleaseUnreferencedAsync(Guid electionId, IReadOnlyCollection<Guid> keepIds)
            => _images.DeleteByElectionExceptAsync(electionId, keepIds.Distinct().ToList());

        public async Task<int> DeleteUnclaimedDraftsAsync()
        {
            var cutoff = DateTime.UtcNow.AddHours(-ValidationRules.ImageDraftLifetimeHours);
            var removed = await _images.DeleteUnclaimedOlderThanAsync(cutoff);

            if (removed > 0)
                _logger.LogInformation("Discarded {Count} unclaimed image draft(s).", removed);

            return removed;
        }

        /// <summary>
        /// Decodes, shrinks and re-encodes the upload as WebP, returning null when the bytes are
        /// not a readable image. Requiring a real decode is what keeps SVG and polyglot files out.
        /// </summary>
        private ProcessedImage? TryProcess(byte[] raw, out ErrorCode rejection)
        {
            rejection = ErrorCode.InvalidImage;

            SKBitmap? decoded = null;
            SKBitmap? scaled = null;
            try
            {
                // Via SKCodec rather than SKBitmap.Decode(byte[]), which throws on input the
                // codec simply reports as unrecognisable.
                using var data = SKData.CreateCopy(raw);
                using var codec = SKCodec.Create(data);
                if (codec is null)
                    return null;

                // Decompression bomb guard. Must happen here: the header is available before any
                // pixel buffer is allocated, and once decoding starts the damage is done.
                var info = codec.Info;
                if (info.Width <= 0 || info.Height <= 0 ||
                    (long)info.Width * info.Height > ValidationRules.ImageMaxPixels)
                {
                    rejection = ErrorCode.ImageTooLarge;
                    return null;
                }

                decoded = SKBitmap.Decode(codec);
                if (decoded is null)
                    return null;

                var (width, height) = Fit(decoded.Width, decoded.Height, ValidationRules.ImageMaxDimension);

                if (width != decoded.Width || height != decoded.Height)
                {
                    scaled = decoded.Resize(new SKImageInfo(width, height), Sampling);
                    if (scaled is null)
                        return null;
                }

                using var image = SKImage.FromBitmap(scaled ?? decoded);
                using var encoded = image.Encode(SKEncodedImageFormat.Webp, ValidationRules.ImageWebpQuality);
                if (encoded is null)
                    return null;

                return new ProcessedImage(encoded.ToArray(), width, height);
            }
            catch (Exception ex)
            {
                // Malformed input is expected here, not exceptional: a decoder failure means
                // "not an image" and must surface as a rejected upload, never as a 500.
                _logger.LogWarning(ex, "Image upload could not be decoded.");
                return null;
            }
            finally
            {
                scaled?.Dispose();
                decoded?.Dispose();
            }
        }

        private static (int Width, int Height) Fit(int width, int height, int maxEdge)
        {
            if (width <= maxEdge && height <= maxEdge)
                return (width, height);

            var scale = (double)maxEdge / Math.Max(width, height);
            return (Math.Max(1, (int)Math.Round(width * scale)),
                    Math.Max(1, (int)Math.Round(height * scale)));
        }

        private sealed record ProcessedImage(byte[] Content, int Width, int Height);
    }
}
