using System;
using System.Collections.Generic;

namespace Electronic_Election_Management_System.Models
{
    public enum UserRole
    {
        Admin,
        Voter
    }

    public class User
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public string Email { get; set; } = string.Empty;

        public string PasswordHash { get; set; } = string.Empty;

        public UserRole Role { get; set; } = UserRole.Voter;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigatie
        public ICollection<Election> ElectionsCreated { get; set; } = new List<Election>();
        public ICollection<VoteToken> VoteTokens { get; set; } = new List<VoteToken>();
        public ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();
    }
}
