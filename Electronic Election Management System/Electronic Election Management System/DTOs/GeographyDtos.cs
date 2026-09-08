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
    }

    // SYNC: geography.model.ts -> RenameGeographicNodeRequest
    public class RenameGeographicNodeRequest
    {
        [Required, NotWhitespace, StringLength(ValidationRules.LabelNameMaxLength)]
        public string Name { get; set; } = string.Empty;
    }

    // SYNC: geography.model.ts -> MoveGeographicNodeRequest
    public class MoveGeographicNodeRequest
    {
        [Required]
        public Guid ParentId { get; set; }
    }
}
