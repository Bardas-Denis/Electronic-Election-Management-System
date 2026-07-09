using System;

namespace Electronic_Election_Management_System.Models
{
    public class AuditLog
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid UserId { get; set; }
        public User? User { get; set; }

        /// <summary>The election this event is related to, or <c>null</c> for events that are not election-scoped (e.g. login).</summary>
        public Guid? ElectionId { get; set; }
        public Election? Election { get; set; }
    
        public string Action { get; set; } = string.Empty;

        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
