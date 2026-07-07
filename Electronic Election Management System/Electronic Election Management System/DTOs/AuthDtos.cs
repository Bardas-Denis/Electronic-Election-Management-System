using System.ComponentModel.DataAnnotations;

namespace Electronic_Election_Management_System.DTOs
{
    // Corespunde LoginRequest din auth.model.ts (frontend)
    public class LoginRequest
    {
        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string Password { get; set; } = string.Empty;
    }

    // Corespunde RegisterRequest din auth.model.ts (frontend)
    public class RegisterRequest
    {
        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required, MinLength(6)]
        public string Password { get; set; } = string.Empty;
    }

    // Corespunde AuthResponse din auth.model.ts (frontend)
    public class AuthResponse
    {
        public string Token { get; set; } = string.Empty;
        public Guid UserId { get; set; }
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty; // "Admin" | "Voter"
        public DateTime ExpiresAt { get; set; }
    }
}
