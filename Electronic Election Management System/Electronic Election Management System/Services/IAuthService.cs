using Electronic_Election_Management_System.DTOs;

namespace Electronic_Election_Management_System.Services
{
    public interface IAuthService
    {
        /// <summary>
        /// Registers a new user with the given email and password.
        /// </summary>
        /// <param name="request">The registration request.</param>
        Task<ServiceResult<AuthResponse>> RegisterAsync(RegisterRequest request);
        /// <summary>
        /// Logs in a user with the given email and password.
        /// </summary>
        /// <param name="request">The login request.</param>
        Task<ServiceResult<AuthResponse>> LoginAsync(LoginRequest request);
    }
}
