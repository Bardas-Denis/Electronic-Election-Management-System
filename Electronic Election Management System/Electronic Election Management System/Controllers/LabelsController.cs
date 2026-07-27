using System.Security.Claims;
using Electronic_Election_Management_System.DTOs;
using Electronic_Election_Management_System.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Electronic_Election_Management_System.Controllers
{
    /// <summary>
    /// Admin-only endpoints for managing labels and assigning them to users.
    /// </summary>
    [ApiController]
    [Route("api/admin")]
    [Authorize(Roles = "Admin")]
    public class LabelsController : ControllerBase
    {
        private readonly ILabelService _labelService;

        public LabelsController(ILabelService labelService)
        {
            _labelService = labelService;
        }

        // ── Label endpoints ────────────────────────────────────────────────────

        /// <summary>Returns all labels.</summary>
        [HttpGet("labels")]
        public async Task<ActionResult<List<LabelDto>>> GetAllLabels()
        {
            var labels = await _labelService.GetAllLabelsAsync();
            return Ok(labels);
        }

        /// <summary>Creates a new label.</summary>
        /// <param name="request">The label to create.</param>
        [HttpPost("labels")]
        public async Task<ActionResult<LabelDto>> CreateLabel(CreateLabelRequest request)
        {
            var result = await _labelService.CreateLabelAsync(request);
            if (!result.Success)
                return BadRequest(new { errorCode = result.ErrorCode });
            return Created(string.Empty, result.Data);
        }

        /// <summary>Deletes a label and removes all its user assignments.</summary>
        /// <param name="id">The id of the label to delete.</param>
        [HttpDelete("labels/{id:guid}")]
        public async Task<IActionResult> DeleteLabel(Guid id)
        {
            var result = await _labelService.DeleteLabelAsync(id);
            if (!result.Success)
                return result.IsNotFound
                    ? NotFound(new { errorCode = result.ErrorCode })
                    : BadRequest(new { errorCode = result.ErrorCode });
            return NoContent();
        }

        /// <summary>Returns all users that have a given label (for admin segmentation).</summary>
        /// <param name="id">The id of the label.</param>
        [HttpGet("labels/{id:guid}/users")]
        public async Task<ActionResult<List<UserWithLabelDto>>> GetUsersWithLabel(Guid id)
        {
            var result = await _labelService.GetUsersWithLabelAsync(id);
            if (!result.Success)
                return result.IsNotFound
                    ? NotFound(new { errorCode = result.ErrorCode })
                    : BadRequest(new { errorCode = result.ErrorCode });
            return Ok(result.Data);
        }

        // ── User–label assignment endpoints ────────────────────────────────────

        /// <summary>Returns all labels assigned to a specific user.</summary>
        /// <param name="id">The id of the user.</param>
        [HttpGet("users/{id:guid}/labels")]
        public async Task<ActionResult<List<UserLabelDto>>> GetUserLabels(Guid id)
        {
            var result = await _labelService.GetUserLabelsAsync(id);
            if (!result.Success)
                return result.IsNotFound
                    ? NotFound(new { errorCode = result.ErrorCode })
                    : BadRequest(new { errorCode = result.ErrorCode });
            return Ok(result.Data);
        }

        /// <summary>Assigns one or more labels to a user.</summary>
        /// <param name="id">The id of the user.</param>
        /// <param name="request">The label ids to assign.</param>
        [HttpPost("users/{id:guid}/labels")]
        public async Task<ActionResult<List<UserLabelDto>>> AssignLabels(
            Guid id, AssignLabelsRequest request)
        {
            var result = await _labelService.AssignLabelsToUserAsync(id, request, GetCurrentUserId());
            if (!result.Success)
                return result.IsNotFound
                    ? NotFound(new { errorCode = result.ErrorCode })
                    : BadRequest(new { errorCode = result.ErrorCode });
            return Ok(result.Data);
        }

        /// <summary>Removes a specific label from a user.</summary>
        /// <param name="id">The id of the user.</param>
        /// <param name="labelId">The id of the label to remove.</param>
        [HttpDelete("users/{id:guid}/labels/{labelId:guid}")]
        public async Task<IActionResult> RemoveUserLabel(Guid id, Guid labelId)
        {
            var result = await _labelService.RemoveLabelFromUserAsync(id, labelId);
            if (!result.Success)
                return result.IsNotFound
                    ? NotFound(new { errorCode = result.ErrorCode })
                    : BadRequest(new { errorCode = result.ErrorCode });
            return NoContent();
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        private Guid GetCurrentUserId()
        {
            string? idClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.Parse(idClaim!);
        }
    }
}
