using System.Security.Claims;
using Electronic_Election_Management_System.DTOs;
using Electronic_Election_Management_System.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Electronic_Election_Management_System.Controllers
{
    /// <summary>
    /// Exposes the vote-casting endpoint used by the "Votează" flow.
    /// </summary>
    [ApiController]
    [Route("api/voting/votes")]
    [Authorize]
    public class VotesController : ControllerBase
    {
        private readonly IVoteService _voteService;

        public VotesController(IVoteService voteService)
        {
            _voteService = voteService;
        }

        /// <summary>
        /// Casts a vote. For non-anonymous elections, <c>voterDeclaration</c> must be supplied with
        /// the fields required by the election's Type (Politic vs Comercial). Anonymous elections
        /// ignore/should omit <c>voterDeclaration</c> entirely.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> CastVote(CastVoteRequest request)
        {
            var result = await _voteService.CastVoteAsync(request, GetCurrentUserId());
            if (!result.Success)
                return result.IsNotFound
                    ? NotFound(new { errorCode = result.ErrorCode })
                    : BadRequest(new { errorCode = result.ErrorCode });
            return Ok();
        }

        /// <summary>
        /// Edits the current user's existing vote for an election. Allowed only once per voter
        /// per election, shared with the delete budget (see VoterChangeRecord / VoteService),
        /// and only while the election is still open for voting.
        /// </summary>
        [HttpPut("{electionId:guid}")]
        public async Task<IActionResult> UpdateVote(Guid electionId, CastVoteRequest request)
        {
            if (electionId != request.ElectionId)
                return BadRequest(new { message = "Route election id does not match the request body." });

            var result = await _voteService.UpdateVoteAsync(request, GetCurrentUserId());
            if (!result.Success)
                return result.IsNotFound
                    ? NotFound(new { errorCode = result.ErrorCode })
                    : BadRequest(new { errorCode = result.ErrorCode });
            return Ok();
        }

        /// <summary>
        /// Deletes the current user's vote for an election, freeing them to vote again.
        /// Consumes the same one-time change budget as editing does - see VoterChangeRecord.
        /// </summary>
        [HttpDelete("{electionId:guid}")]
        public async Task<IActionResult> DeleteVote(Guid electionId)
        {
            var result = await _voteService.DeleteVoteAsync(electionId, GetCurrentUserId());
            if (!result.Success)
                return result.IsNotFound
                    ? NotFound(new { errorCode = result.ErrorCode })
                    : BadRequest(new { errorCode = result.ErrorCode });
            return Ok();
        }

        /// <summary>Returns the current user's vote for an election, if any.</summary>
        [HttpGet("{electionId:guid}/me")]
        public async Task<IActionResult> GetMyVote(Guid electionId)
        {
            var result = await _voteService.GetMyVoteAsync(electionId, GetCurrentUserId());
            if (!result.Success)
                return result.IsNotFound
                    ? NotFound(new { errorCode = result.ErrorCode })
                    : BadRequest(new { errorCode = result.ErrorCode });
            return Ok(result.Data);
        }

        private Guid GetCurrentUserId()
        {
            string? idClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.Parse(idClaim!);
        }
    }
}
