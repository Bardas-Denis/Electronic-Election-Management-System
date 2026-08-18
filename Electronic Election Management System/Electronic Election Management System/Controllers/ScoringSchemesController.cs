using System.Security.Claims;
using Electronic_Election_Management_System.DTOs;
using Electronic_Election_Management_System.Services.interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Electronic_Election_Management_System.Controllers
{
    [ApiController]
    [Route("api/scoring-schemes")]
    [Authorize(Roles = "Admin,ElectionManager")]
    public class ScoringSchemesController : ControllerBase
    {
        private readonly IScoringSchemeService _scoringSchemeService;

        public ScoringSchemesController(IScoringSchemeService scoringSchemeService)
        {
            _scoringSchemeService = scoringSchemeService;
        }

        [HttpGet]
        public async Task<ActionResult<List<ScoringSchemeDto>>> GetAll()
        {
            var schemes = await _scoringSchemeService.GetSchemesAsync(GetCurrentUserId());
            return Ok(schemes);
        }

        [HttpPost]
        public async Task<ActionResult<ScoringSchemeDto>> Create(CreateScoringSchemeDto request)
        {
            var scheme = await _scoringSchemeService.CreateSchemeAsync(request, GetCurrentUserId());
            return CreatedAtAction(nameof(GetAll), new { }, scheme);
        }

        private Guid GetCurrentUserId()
        {
            string? idClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.Parse(idClaim!);
        }
    }
}
