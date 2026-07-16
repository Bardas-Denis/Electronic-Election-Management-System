using Electronic_Election_Management_System.Services;
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
            var results = await _resultsService.GetResultsAsync(electionId);
            if (results is null)
                return NotFound();
            return Ok(results);
        }
    }
}