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
        /// <summary>
        /// ISO 3166-1 alpha-2 code of the country of residence ("RO"), matching
        /// <see cref="Label.Code"/> rather than a display name. Names are translated and
        /// edited, codes are not, so this keeps pointing at the right node of the geographic
        /// tree after a rename or a re-seed.
        /// </summary>
        public string? ResidenceCountry { get; set; }
        public string? ResidenceCounty { get; set; }
        /// <summary>
        /// ISO 3166-2 code of the subdivision picked from the geographic tree ("RO-CJ").
        /// Kept alongside <see cref="ResidenceCounty"/> rather than replacing it: the name is
        /// still what a Politic vote declaration carries, and it is auto-filled from the CNP,
        /// which yields a Romanian name and no ISO code.
        /// </summary>
        public string? ResidenceCountyCode { get; set; }
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
