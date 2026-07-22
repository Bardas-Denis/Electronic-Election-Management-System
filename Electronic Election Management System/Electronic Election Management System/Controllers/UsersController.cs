using System.Security.Claims;
using Electronic_Election_Management_System.DTOs;
using Electronic_Election_Management_System.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Electronic_Election_Management_System.Controllers
{
    /// <summary>
    /// Exposes user management endpoints for administrators.
    /// </summary>
    [ApiController]
    [Route("api/users")]
    [Authorize(Roles = "Admin")]
    public class UsersController : ControllerBase
    {
        private readonly IUserService _userService;

        public UsersController(IUserService userService)
        {
            _userService = userService;
        }

        /// <summary>
        /// Retrieves all users.
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<List<UserDto>>> GetAll()
        {
            var users = await _userService.GetAllAsync();
            return Ok(users);
        }

        /// <summary>
        /// Updates the role of a specific user.
        /// </summary>
        /// <param name="id">The id of the user to update.</param>
        /// <param name="request">The role update request.</param>
        [HttpPut("{id:guid}/role")]
        public async Task<ActionResult<UserDto>> UpdateRole(Guid id, UpdateUserRoleRequest request)
        {
            // Check if the person trying to update the role is allowed to do it
            var result = await _userService.UpdateRoleAsync(id, request, GetCurrentUserId());
            if (!result.Success)
                return result.IsNotFound
                    ? NotFound(new { errorCode = result.ErrorCode })
                    : BadRequest(new { errorCode = result.ErrorCode });
            return Ok(result.Data);
        }

        /// <summary>
        /// Deletes a user.
        /// </summary>
        /// <param name="id">The id of the user to delete.</param>
        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var result = await _userService.DeleteAsync(id, GetCurrentUserId());
            if (!result.Success)
                return result.IsNotFound
                    ? NotFound(new { errorCode = result.ErrorCode })
                    : BadRequest(new { errorCode = result.ErrorCode });
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
