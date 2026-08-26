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
        /// Who voted for what, grouped by option. Kept off the results payload on purpose - that
        /// one is broadcast over SignalR to every subscriber in the election group.
        /// </summary>
        /// <param name="questionId">Omit for an election whose options hang off the election
        /// itself rather than off a question.</param>
        [HttpGet("{electionId:guid}/voters")]
        public async Task<IActionResult> GetVoters(Guid electionId, [FromQuery] Guid? questionId)
        {
            var result = await _resultsService.GetVotersAsync(electionId, questionId, GetCurrentUserId());
            if (result.Success)
                return Ok(result.Data);

            return result.IsNotFound
                ? NotFound(new { errorCode = result.ErrorCode })
                : BadRequest(new { errorCode = result.ErrorCode });
        }

        /// <summary>
        /// Who wrote each typed answer on one question - a FreeText question's answers, or a
        /// Choice question's "Other" ones. Text and author come back paired.
        /// </summary>
        [HttpGet("{electionId:guid}/questions/{questionId:guid}/text-answers")]
        public async Task<IActionResult> GetTextAnswerAuthors(Guid electionId, Guid questionId)
        {
            var result = await _resultsService.GetTextAnswerAuthorsAsync(electionId, questionId, GetCurrentUserId());
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
