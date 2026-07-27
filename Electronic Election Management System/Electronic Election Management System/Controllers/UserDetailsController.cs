using System.Security.Claims;
using Electronic_Election_Management_System.DTOs;
using Electronic_Election_Management_System.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Electronic_Election_Management_System.Controllers
{
    /// <summary>
    /// Exposes endpoints for managing the authenticated user's profile details.
    /// </summary>
    [ApiController]
    [Route("api/me/details")]
    [Authorize]
    public class UserDetailsController : ControllerBase
    {
        private readonly IUserService _userService;

        public UserDetailsController(IUserService userService)
        {
            _userService = userService;
        }

        /// <summary>
        /// Returns the current user's saved personal details.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetMyDetails()
        {
            var dto = await _userService.GetMyDetailsAsync(GetCurrentUserId());
            if (dto is null)
                return NoContent();
            return Ok(dto);
        }

        /// <summary>
        /// Saves the current user's personal details.
        /// </summary>
        [HttpPut]
        public async Task<IActionResult> SaveMyDetails(PersonalDetailsDto dto)
        {
            var result = await _userService.SaveMyDetailsAsync(GetCurrentUserId(), dto);
            if (!result.Success)
                return BadRequest(new { errorCode = result.ErrorCode });
            return Ok(result.Data);
        }

        private Guid GetCurrentUserId()
        {
            string? idClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.Parse(idClaim!);
        }
    }
}
