using System.ComponentModel.DataAnnotations;

namespace Electronic_Election_Management_System.DTOs
{
    // SYNC: auth.model.ts -> LoginRequest
    public class LoginRequest
    {
        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string Password { get; set; } = string.Empty;
    }

    // SYNC: auth.model.ts -> RegisterRequest
    public class RegisterRequest
    {
        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required, MinLength(6)]
        public string Password { get; set; } = string.Empty;
    }

    // SYNC: auth.model.ts -> AuthResponse
    public class AuthResponse
    {
        public string Token { get; set; } = string.Empty;
        public Guid UserId { get; set; }
        public string Email { get; set; } = string.Empty;
        /// <summary>The role assigned to the authenticated user. Valid values: <c>"Admin"</c> or <c>"Voter"</c>.</summary>
        public string Role { get; set; } = string.Empty; // "Admin" | "Voter"
        public DateTime ExpiresAt { get; set; }
    }
}
