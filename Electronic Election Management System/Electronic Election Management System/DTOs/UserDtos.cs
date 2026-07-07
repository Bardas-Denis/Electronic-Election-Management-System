using System.ComponentModel.DataAnnotations;

namespace Electronic_Election_Management_System.DTOs
{
    // Folosit de admin pentru a lista utilizatorii (fara PasswordHash!)
    public class UserDto
    {
        public Guid Id { get; set; }
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty; // "Admin" | "Voter"
        public DateTime CreatedAt { get; set; }
    }

    // Body pentru schimbarea rolului unui utilizator (doar Admin)
    public class UpdateUserRoleRequest
    {
        [Required]
        public string Role { get; set; } = string.Empty; // "Admin" | "Voter"
    }

    // Folosit de admin pentru vizualizarea auditului
    public class AuditLogDto
    {
        public Guid Id { get; set; }
        public string UserEmail { get; set; } = string.Empty;
        public string? ElectionTitle { get; set; }
        public string Action { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
    }
}
