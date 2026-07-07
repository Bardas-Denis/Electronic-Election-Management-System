using System;
using System.Collections.Generic;

namespace Electronic_Election_Management_System.Models
{
    public enum ElectionType
    {
        Politic,
        Comercial
    }

    public class Election
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid CreatedByUserId { get; set; }
        public User? CreatedByUser { get; set; }

        public string Title { get; set; } = string.Empty;

        public string? Description { get; set; }

        public ElectionType Type { get; set; }

        // Daca e true: se foloseste fluxul VoteToken (fara legatura user->vot).
        // Daca e false: Votes.UserId se completeaza direct.
        public bool IsAnonymous { get; set; } = true;

        public DateTime StartsAt { get; set; }
        public DateTime EndsAt { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigatie
        public ICollection<Option> Options { get; set; } = new List<Option>();
        public ICollection<VoteToken> VoteTokens { get; set; } = new List<VoteToken>();
        public ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();
    }
}
