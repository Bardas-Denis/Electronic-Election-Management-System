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
                    ? NotFound(new { message = result.Error })
                    : BadRequest(new { message = result.Error });
            return Ok();
        }

        private Guid GetCurrentUserId()
        {
            string? idClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.Parse(idClaim!);
        }
    }
}
