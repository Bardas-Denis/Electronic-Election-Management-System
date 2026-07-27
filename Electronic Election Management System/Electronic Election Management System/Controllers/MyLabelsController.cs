using System.Security.Claims;
using Electronic_Election_Management_System.DTOs;
using Electronic_Election_Management_System.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Electronic_Election_Management_System.Controllers
{
    /// <summary>
    /// Exposes read-only label endpoints for the authenticated user.
    /// Regular users can see their own labels but cannot modify them.
    /// </summary>
    [ApiController]
    [Route("api/me/labels")]
    [Authorize]
    public class MyLabelsController : ControllerBase
    {
        private readonly ILabelService _labelService;

        public MyLabelsController(ILabelService labelService)
        {
            _labelService = labelService;
        }

        /// <summary>
        /// Returns the labels assigned to the currently authenticated user.
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<List<UserLabelDto>>> GetMyLabels()
        {
            var result = await _labelService.GetMyLabelsAsync(GetCurrentUserId());
            // GetMyLabelsAsync never fails (authenticated user always exists)
            return Ok(result.Data);
        }

        private Guid GetCurrentUserId()
        {
            string? idClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.Parse(idClaim!);
        }
    }
}
