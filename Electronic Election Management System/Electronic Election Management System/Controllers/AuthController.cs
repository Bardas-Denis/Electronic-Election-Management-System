using Electronic_Election_Management_System.DTOs;
using Electronic_Election_Management_System.Services;
using Microsoft.AspNetCore.Mvc;

namespace Electronic_Election_Management_System.Controllers
{
    /// <summary>
    /// Handles user authentication and registration.
    /// </summary>
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;

        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }

        /// <summary>
        /// Registers a new user.
        /// </summary>
        /// <param name="request">The registration request.</param>
        [HttpPost("register")]
        public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request)
        {
            var result = await _authService.RegisterAsync(request);
            if (!result.Success)
                return Conflict(new { errorCode = result.ErrorCode });
            return Ok(result.Data);
        }

        /// <summary>
        /// Logs in a user.
        /// </summary>
        /// <param name="request">The login request.</param>
        [HttpPost("login")]
        public async Task<ActionResult<AuthResponse>> Login(LoginRequest request)
        {
            var result = await _authService.LoginAsync(request);
            if (!result.Success)
                return Unauthorized(new { errorCode = result.ErrorCode });
            return Ok(result.Data);
        }
    }
}
