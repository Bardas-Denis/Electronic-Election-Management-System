using System;
using System.ComponentModel.DataAnnotations;

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

        /// <summary>
        /// What the place is called in words — "County", "City", "Județ". Descriptive only;
        /// <see cref="Category"/> is what carries the tree's structure.
        /// </summary>
        public string? Kind { get; set; }

        public bool HasChildren { get; set; }
    }

    // SYNC: geography.model.ts -> CreateGeographicNodeRequest
    /// <summary>
    /// Adds a node under an existing one. The category is derived from the parent rather than
    /// supplied: a country's child is a subdivision, anything deeper is a locality.
    /// </summary>
    public class CreateGeographicNodeRequest
    {
        [Required]
        public Guid ParentId { get; set; }

        [Required, NotWhitespace, StringLength(ValidationRules.LabelNameMaxLength)]
        public string Name { get; set; } = string.Empty;

        /// <summary>What the place is: "Oraș", "Comună", "Sector". Optional and free-form.</summary>
        [StringLength(ValidationRules.LabelCategoryMaxLength)]
        public string? Kind { get; set; }
    }

    // SYNC: geography.model.ts -> UpdateGeographicNodeRequest
    /// <summary>
    /// Edits the two things about a node that are safe to change by hand. Its code and its place
    /// in the tree stay put, so nothing that points at this node is disturbed.
    /// </summary>
    public class UpdateGeographicNodeRequest
    {
        [Required, NotWhitespace, StringLength(ValidationRules.LabelNameMaxLength)]
        public string Name { get; set; } = string.Empty;

        /// <summary>Blank clears it back to "unspecified".</summary>
        [StringLength(ValidationRules.LabelCategoryMaxLength)]
        public string? Kind { get; set; }
    }

    // SYNC: geography.model.ts -> MoveGeographicNodeRequest
    public class MoveGeographicNodeRequest
    {
        [Required]
        public Guid ParentId { get; set; }
    }
}
