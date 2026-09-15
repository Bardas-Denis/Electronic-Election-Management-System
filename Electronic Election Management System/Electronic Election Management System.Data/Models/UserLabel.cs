using System;

namespace Electronic_Election_Management_System.Models
{
    /// <summary>
    /// Join entity between <see cref="User"/> and <see cref="Label"/>.
    /// Carries two extra auditing columns (<see cref="AssignedBy"/> and <see cref="AssignedAt"/>)
    /// which is why a dedicated C# class is required rather than EF Core's implicit
    /// many-to-many mapping.
    /// </summary>
    public class UserLabel
    {
        public Guid UserId { get; set; }

        public Guid LabelId { get; set; }

        /// <summary>
        /// Who caused the assignment: the administrator who handed the label out, or - for a
        /// geographic label - the user themselves, since it follows from their own profile.
        /// </summary>
        public Guid AssignedBy { get; set; }

        public DateTime AssignedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public User User { get; set; } = null!;
        public Label Label { get; set; } = null!;

        /// <summary>The admin user who performed the assignment.</summary>
        public User Admin { get; set; } = null!;
    }
}
