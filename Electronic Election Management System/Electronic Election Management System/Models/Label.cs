using System;
using System.Collections.Generic;

namespace Electronic_Election_Management_System.Models
{
    public class Label
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>Display name for the label (e.g. "Siemens", "football", "Cluj").</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Optional grouping category (e.g. "employer", "interest", "nationality").</summary>
        public string? Category { get; set; }

        /// <summary>
        /// Stable external identifier, currently the ISO code for geographic labels:
        /// ISO 3166-1 alpha-2 for a country ("RO"), ISO 3166-2 for a subdivision ("RO-CJ").
        /// Null for labels that are not geographic. Names are translated and change over time,
        /// codes do not, so seeding and re-seeding match on this rather than on <see cref="Name"/>.
        /// </summary>
        public string? Code { get; set; }

        /// <summary>
        /// Parent in the geographic tree — a subdivision points at its country. Null for a root:
        /// every country, and every label that is not part of a tree at all.
        /// </summary>
        public Guid? ParentId { get; set; }
        public Label? Parent { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public ICollection<Label> Children { get; set; } = new List<Label>();
        public ICollection<UserLabel> UserLabels { get; set; } = new List<UserLabel>();
    }
}
