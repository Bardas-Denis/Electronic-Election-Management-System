namespace Electronic_Election_Management_System.Models
{
    /// <summary>
    /// User-editable profile details that survive across elections.
    /// </summary>
    public class UserDetails
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid UserId { get; set; }
        public User? User { get; set; }

        // --- Politic ---
        /// <summary>Raw Romanian CNP.</summary>
        public string? Cnp { get; set; }
        /// <summary>Derived from Cnp server-side, never client-supplied.</summary>
        public DateOnly? BirthDate { get; set; }
        public string? FullName { get; set; }
        public string? ResidenceCounty{ get; set; }
        public string? ResidenceAddress { get; set; }
        public string? ResidenceCity { get; set; }
        public string? Citizenship { get; set; }

        // --- Comercial / shared ---
        /// <summary>"M" or "F". Self-declared by the user; not derived from CNP here.</summary>
        public string? Gender { get; set; }

        // --- Comercial ---
        public string? WorkEmail { get; set; }
        public string? EmployeeId { get; set; }
        public string? Department { get; set; }
        public string? JobTitle { get; set; }
        public string? Company { get; set; }

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
