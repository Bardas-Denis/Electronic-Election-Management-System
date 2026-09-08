using System;

namespace Electronic_Election_Management_System.DTOs
{
    // SYNC: geography.model.ts -> GeographicNodeDto
    /// <summary>
    /// One node of the geographic label tree as the client sees it. <see cref="Code"/> is the
    /// stable identity — names are translated and edited, codes are not — and
    /// <see cref="HasChildren"/> tells a picker whether another level exists below.
    /// </summary>
    public class GeographicNodeDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Code { get; set; }
        public string? Category { get; set; }
        public bool HasChildren { get; set; }
    }
}
