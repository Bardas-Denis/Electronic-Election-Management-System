using System.ComponentModel.DataAnnotations;

namespace Electronic_Election_Management_System.DTOs
{
    // SYNC: user.model.ts -> UserDto
    public class UserDto
    {
        public Guid Id { get; set; }
        public string Email { get; set; } = string.Empty;
        /// <summary>The user's role. Valid values: <c>"Admin"</c> or <c>"Voter"</c>.</summary>
        public string Role { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }

    // SYNC: user.model.ts -> UpdateUserRoleRequest
    public class UpdateUserRoleRequest
    {
        [Required]
        /// <summary>The role to assign to the user. Valid values: <c>"Admin"</c> or <c>"Voter"</c>.</summary>
        public string Role { get; set; } = string.Empty;
    }

    public class AuditLogDto
    {
        public Guid Id { get; set; }
        public string UserEmail { get; set; } = string.Empty;
        public string? ElectionTitle { get; set; }
        public string Action { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
    }
}
