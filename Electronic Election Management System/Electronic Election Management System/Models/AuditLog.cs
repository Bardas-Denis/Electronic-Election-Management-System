using System;

namespace Electronic_Election_Management_System.Models
{
    // Inregistreaza EVENIMENTE (cine, ce actiune, cand) - niciodata continutul votului.
    public class AuditLog
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid UserId { get; set; }
        public User? User { get; set; }

        public Guid? ElectionId { get; set; }
        public Election? Election { get; set; }

        // ex: "login", "a_creat_alegere", "a_votat"
        public string Action { get; set; } = string.Empty;

        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
