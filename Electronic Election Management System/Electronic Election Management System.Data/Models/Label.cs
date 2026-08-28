using System;
using System.Collections.Generic;

namespace Electronic_Election_Management_System.Models
{
    public class Label
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>Unique display name for the label (e.g. "Siemens", "football", "Romania").</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Optional grouping category (e.g. "employer", "interest", "nationality").</summary>
        public string? Category { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public ICollection<UserLabel> UserLabels { get; set; } = new List<UserLabel>();
    }
}
