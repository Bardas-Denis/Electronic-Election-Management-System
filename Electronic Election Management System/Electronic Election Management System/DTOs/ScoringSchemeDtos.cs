using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Electronic_Election_Management_System.DTOs
{
    public class ScoringSchemeDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public List<int> Points { get; set; } = new();
        public bool IsLinear { get; set; }
        public bool IsPredefined { get; set; }

        /// <summary>Non-null when the points are produced by a scoring plugin.</summary>
        public string? PluginKey { get; set; }
    }

    public class CreateScoringSchemeDto
    {
        [Required, MinLength(1), MaxLength(100)]
        public string Name { get; set; } = string.Empty;
        
        [Required]
        public List<int> Points { get; set; } = new();
    }
}
