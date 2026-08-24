using Electronic_Election_Management_System.Services;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Electronic_Election_Management_System.Controllers
{
    [ApiController]
    [Route("api/results")]
    [Authorize]
    public class ResultsController : ControllerBase
    {
        private readonly IResultsService _resultsService;

        public ResultsController(IResultsService resultsService)
        {
            _resultsService = resultsService;
        }

        [HttpGet("{electionId:guid}")]
        public async Task<IActionResult> GetResults(Guid electionId)
        {
            var results = await _resultsService.GetResultsAsync(electionId, GetCurrentUserId());
            if (results is null)
                return NotFound();
            return Ok(results);
        }

        /// <summary>
        /// Who voted for one option. Kept off the results payload on purpose - that one is
        /// broadcast over SignalR to every subscriber in the election group.
        /// </summary>
        [HttpGet("{electionId:guid}/options/{optionId:guid}/voters")]
        public async Task<IActionResult> GetOptionVoters(Guid electionId, Guid optionId)
        {
            var result = await _resultsService.GetOptionVotersAsync(electionId, optionId, GetCurrentUserId());
            if (result.Success)
                return Ok(result.Data);

            return result.IsNotFound
                ? NotFound(new { errorCode = result.ErrorCode })
                : BadRequest(new { errorCode = result.ErrorCode });
        }

        private Guid GetCurrentUserId()
            => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    }
}
