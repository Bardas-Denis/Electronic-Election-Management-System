using System.Security.Claims;
using Electronic_Election_Management_System.DTOs;
using Electronic_Election_Management_System.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Electronic_Election_Management_System.Controllers
{
    // Toate endpoint-urile de aici sunt exclusiv pentru Administrator (gestionarea utilizatorilor si rolurilor).
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

        // GET /api/users
        [HttpGet]
        public async Task<ActionResult<List<UserDto>>> GetAll()
        {
            var users = await _userService.GetAllAsync();
            return Ok(users);
        }

        // PUT /api/users/{id}/role
        [HttpPut("{id:guid}/role")]
        public async Task<ActionResult<UserDto>> UpdateRole(Guid id, UpdateUserRoleRequest request)
        {
            var result = await _userService.UpdateRoleAsync(id, request, GetCurrentUserId());
            if (!result.Success)
                return result.IsNotFound
                    ? NotFound(new { message = result.Error })
                    : BadRequest(new { message = result.Error });
            return Ok(result.Data);
        }

        // DELETE /api/users/{id}
        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var result = await _userService.DeleteAsync(id, GetCurrentUserId());
            if (!result.Success)
                return result.IsNotFound
                    ? NotFound(new { message = result.Error })
                    : BadRequest(new { message = result.Error });
            return NoContent();
        }

        private Guid GetCurrentUserId()
        {
            string? idClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.Parse(idClaim!);
        }
    }
}
