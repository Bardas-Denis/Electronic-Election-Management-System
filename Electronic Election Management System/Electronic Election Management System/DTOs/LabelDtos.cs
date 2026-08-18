using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Electronic_Election_Management_System.DTOs
{
    // SYNC: label.model.ts -> LabelDto
    public class LabelDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Category { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    // SYNC: label.model.ts -> UserLabelDto
    /// <summary>A label as seen on a specific user's profile (includes assignment metadata).</summary>
    public class UserLabelDto
    {
        public Guid LabelId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Category { get; set; }
        public Guid AssignedBy { get; set; }
        public DateTime AssignedAt { get; set; }
    }

    // SYNC: label.model.ts -> UserWithLabelDto
    /// <summary>A user entry as seen from a label's perspective (for admin segmentation).</summary>
    public class UserWithLabelDto
    {
        public Guid UserId { get; set; }
        public string Email { get; set; } = string.Empty;
        public DateTime AssignedAt { get; set; }
    }

    // SYNC: label.model.ts -> CreateLabelRequest
    public class CreateLabelRequest
    {
        [Required, NotWhitespace, StringLength(ValidationRules.LabelNameMaxLength)]
        public string Name { get; set; } = string.Empty;

        [StringLength(ValidationRules.LabelCategoryMaxLength)]
        public string? Category { get; set; }
    }

    // SYNC: label.model.ts -> AssignLabelsRequest
    public class AssignLabelsRequest
    {
        [Required]
        [MinLength(1)]
        public List<Guid> LabelIds { get; set; } = new();
    }
}
