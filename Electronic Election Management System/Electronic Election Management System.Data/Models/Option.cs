using System;
using System.Collections.Generic;

namespace Electronic_Election_Management_System.Models
{
    //Option inside an election
    public class Option
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid ElectionId { get; set; }
        public Election? Election { get; set; }
        public Guid? QuestionId { get; set; }
        public ElectionQuestion? Question { get; set; }

        public string Label { get; set; } = string.Empty;

        public string? Description { get; set; }

        /// <summary>Superseded by <see cref="ImageId"/>. Kept only until the backfill has run
        /// on every environment; the column is dropped by a follow-up migration.</summary>
        public string? ImageDataUrl { get; set; }

        /// <summary>The option's picture, stored out-of-row in <see cref="ElectionImage"/>.</summary>
        public Guid? ImageId { get; set; }
        public ElectionImage? Image { get; set; }

        public ICollection<Vote> Votes { get; set; } = new List<Vote>();
    }
}
