using System.Security.Claims;
using Electronic_Election_Management_System.Constants;
using Electronic_Election_Management_System.DTOs;
using Electronic_Election_Management_System.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Electronic_Election_Management_System.Controllers
{
    /// <summary>
    /// Exposes election-related endpoints for voters and administrators.
    /// </summary>
    [ApiController]
    [Route("api/voting/elections")]
    [Authorize]
    public class ElectionsController : ControllerBase
    {
        private readonly IElectionService _electionService;

        public ElectionsController(IElectionService electionService)
        {
            _electionService = electionService;
        }

        /// <summary>
        /// Retrieves all elections (voting list can be seen by any authenticated role).
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<List<ElectionDto>>> GetAll()
        {
            var elections = await _electionService.GetAllAsync(GetCurrentUserId());
            return Ok(elections);
        }

        /// <summary>
        /// Retrieves only the elections created by the current user (management view).
        /// </summary>
        [HttpGet("mine")]
        [Authorize(Roles = "Admin,ElectionManager")]
        public async Task<ActionResult<List<ElectionDto>>> GetMine()
        {
            var elections = await _electionService.GetCreatedByAsync(GetCurrentUserId());
            return Ok(elections);
        }

        /// <summary>
        /// Returns the minimal registered-user list needed by the manual invitation picker.
        /// </summary>
        [HttpGet("invitation-candidates")]
        [Authorize(Roles = "Admin,ElectionManager")]
        public async Task<ActionResult<List<InvitationCandidateDto>>> GetInvitationCandidates()
        {
            var candidates = await _electionService.GetInvitationCandidatesAsync(GetCurrentUserId());
            return Ok(candidates);
        }

        /// <summary>
        /// Retrieve election based on id.
        /// </summary>
        /// <param name="id">The id of the election to retrieve.</param>
        [HttpGet("{id:guid}")]
        public async Task<ActionResult<ElectionDto>> GetById(Guid id)
        {
            var election = await _electionService.GetByIdAsync(id, GetCurrentUserId());
            if (election is null)
                return NotFound();
            return Ok(election);
        }

        /// <summary>
        /// Create a new election.
        /// </summary>
        /// <param name="request">The election to create.</param>
        [HttpPost]
        [Authorize(Roles = "Admin,ElectionManager")]
        public async Task<ActionResult<ElectionDto>> Create(CreateElectionRequest request)
        {
            var result = await _electionService.CreateAsync(request, GetCurrentUserId());
            if (!result.Success)
                return BadRequest(new { errorCode = result.ErrorCode });
            return CreatedAtAction(nameof(GetById), new { id = result.Data!.Id }, result.Data);
        }

        /// <summary>
        /// Update election based on id.
        /// </summary>
        /// <param name="id">The id of the election to update.</param>
        /// <param name="request">The election to update.</param>
        [HttpPut("{id:guid}")]
        [Authorize(Roles = "Admin,ElectionManager")]
        public async Task<ActionResult<ElectionDto>> Update(Guid id, UpdateElectionRequest request)
        {
            var result = await _electionService.UpdateAsync(id, request, GetCurrentUserId());
            if (!result.Success)
                return result.IsNotFound
                    ? NotFound(new { errorCode = result.ErrorCode })
                    : BadRequest(new { errorCode = result.ErrorCode });
            return Ok(result.Data);
        }

        /// <summary>
        /// Delete election based on id.
        /// </summary>
        /// <param name="id">The id of the election to delete.</param>
        [HttpDelete("{id:guid}")]
        [Authorize(Roles = "Admin,ElectionManager")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var result = await _electionService.DeleteAsync(id, GetCurrentUserId());
            if (!result.Success)
                return result.IsNotFound
                    ? NotFound(new { errorCode = result.ErrorCode })
                    : BadRequest(new { errorCode = result.ErrorCode });
            return NoContent();
        }

        /// <summary>Lists invitations for a closed election. Only its creator may call this endpoint.</summary>
        [HttpGet("{id:guid}/invitations")]
        [Authorize(Roles = "Admin,ElectionManager")]
        public async Task<ActionResult<List<ElectionInvitationDto>>> GetInvitations(Guid id)
        {
            var result = await _electionService.GetInvitationsAsync(id, GetCurrentUserId());
            if (!result.Success)
                return result.IsNotFound
                    ? NotFound(new { errorCode = result.ErrorCode })
                    : Forbid();
            return Ok(result.Data);
        }

        /// <summary>
        /// Adds manual invitations by existing user ID and/or invitations by email.
        /// Email invitations also work when the recipient registers after being invited.
        /// </summary>
        [HttpPost("{id:guid}/invitations")]
        [Authorize(Roles = "Admin,ElectionManager")]
        public async Task<ActionResult<List<ElectionInvitationDto>>> Invite(
            Guid id,
            InviteToElectionRequest request)
        {
            var result = await _electionService.InviteAsync(id, request, GetCurrentUserId());
            if (!result.Success)
            {
                if (result.IsNotFound)
                    return NotFound(new { errorCode = result.ErrorCode });
                if (result.ErrorCode == ErrorCode.NotAuthorizedToManageInvitations)
                    return Forbid();
                return BadRequest(new { errorCode = result.ErrorCode });
            }
            return Ok(result.Data);
        }

        /// <summary>Revokes one invitation from a closed election.</summary>
        [HttpDelete("{id:guid}/invitations/{invitationId:guid}")]
        [Authorize(Roles = "Admin,ElectionManager")]
        public async Task<IActionResult> RemoveInvitation(Guid id, Guid invitationId)
        {
            var result = await _electionService.RemoveInvitationAsync(id, invitationId, GetCurrentUserId());
            if (!result.Success)
                return result.IsNotFound
                    ? NotFound(new { errorCode = result.ErrorCode })
                    : Forbid();
            return NoContent();
        }

        /// <summary>
        /// Retrieves the ID of the current user.
        /// </summary>
        private Guid GetCurrentUserId()
        {
            string? idClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.Parse(idClaim!);
        }
    }
}
