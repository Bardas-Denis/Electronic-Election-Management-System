using System;

namespace Electronic_Election_Management_System.Models
{
    /// <summary>
    /// Extra identity/demographic info collected at the moment of voting, in a non-anonymous election.
    /// Never created for anonymous elections (Vote.VoteTokenId is set instead, with no link back to a user).
    /// Which fields are populated depends on the parent Election's <see cref="ElectionType"/>:
    /// Politic uses the Cnp-derived fields, Comercial uses Gender/EmployeeId/Department.
    /// </summary>
    public class VoterDeclaration
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid VoteId { get; set; }
        public Vote? Vote { get; set; }

        // --- Politic elections ---

        /// <summary>
        /// Raw Romanian CNP, kept only for the demo/audit trail of this student project.
        /// In a real deployment this should NOT be stored in plaintext (or at all) once
        /// BirthDate/Gender/DomiciliuJudet have been derived from it - see CnpService.
        /// </summary>
        public string? Cnp { get; set; }
        public string? FullName { get; set; }
        public string? DomiciliuJudet { get; set; }
        public string? DomiciliuAdresa { get; set; }
        public string? DomiciliuLocalitate { get; set; }
        public string? Citizenship { get; set; }
        /// <summary>Derived from the CNP server-side. Never taken as-is from the client.</summary>
        public DateOnly? BirthDate { get; set; }
        /// <summary>"M" or "F". Derived from CNP for Politic elections, self-declared for Comercial ones.</summary>
        public string? Gender { get; set; }

        // --- Comercial elections ---

        public string? EmployeeId { get; set; }
        public string? Department { get; set; }
        public string? WorkEmail { get; set; }
        public string? JobTitle { get; set; }
        public string? Company { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
