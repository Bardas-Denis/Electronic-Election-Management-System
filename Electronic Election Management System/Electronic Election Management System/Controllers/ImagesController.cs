using System.Security.Claims;
using Electronic_Election_Management_System.Constants;
using Electronic_Election_Management_System.DTOs;
using Electronic_Election_Management_System.Services.interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace Electronic_Election_Management_System.Controllers
{
    /// <summary>
    /// Serves ballot images. Pictures are uploaded here first and referenced by id when the
    /// election is saved, so reading an election never carries the bytes with it.
    /// </summary>
    [ApiController]
    [Route("api/images")]
    [Authorize]
    public class ImagesController : ControllerBase
    {
        private readonly IImageService _imageService;

        public ImagesController(IImageService imageService)
        {
            _imageService = imageService;
        }

        /// <summary>
        /// Uploads a picture for an option or a question. The file is re-encoded as WebP, so the
        /// returned dimensions may differ from the original. The image stays unattached until the
        /// election referencing it is saved.
        /// </summary>
        [HttpPost]
        [Authorize(Roles = "Admin,ElectionManager")]
        [RequestSizeLimit(ValidationRules.ImageMaxUploadBytes)]
        public async Task<IActionResult> Upload(IFormFile file)
        {
            if (file is null || file.Length == 0)
                return BadRequest(new { errorCode = ErrorCode.InvalidImage });

            await using var stream = file.OpenReadStream();
            var result = await _imageService.UploadAsync(stream, file.Length, GetCurrentUserId());

            if (!result.Success)
                return BadRequest(new { errorCode = result.ErrorCode });

            return CreatedAtAction(nameof(Get), new { id = result.Data!.Id }, result.Data);
        }

        /// <summary>
        /// Returns the stored image bytes. Responds 404 both when the image does not exist and
        /// when the caller may not see it, so a closed election's contents stay private.
        /// </summary>
        [HttpGet("{id:guid}")]
        public async Task<IActionResult> Get(Guid id)
        {
            // Metadata first: neither a refused request nor one answered from cache may cause the
            // bytes to be read out of the database.
            var result = await _imageService.GetMetadataForUserAsync(id, GetCurrentUserId());
            if (!result.Success)
                return NotFound(new { errorCode = result.ErrorCode });

            var metadata = result.Data!;
            var entityTag = new EntityTagHeaderValue($"\"{metadata.Sha256}\"");

            // Safe to mark immutable: replacing a picture creates a new row with a new id, so the
            // content behind this URL cannot change.
            Response.Headers.CacheControl = "private, max-age=31536000, immutable";
            Response.Headers.ETag = entityTag.ToString();

            if (Request.Headers.IfNoneMatch.Any(candidate => candidate == entityTag.ToString()))
                return StatusCode(StatusCodes.Status304NotModified);

            var content = await _imageService.GetContentAsync(id);
            if (content is null)
                return NotFound(new { errorCode = ErrorCode.ResourceNotFound });

            return File(content, metadata.ContentType, lastModified: null, entityTag: entityTag);
        }

        private Guid GetCurrentUserId()
        {
            string? idClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.Parse(idClaim!);
        }
    }
}
