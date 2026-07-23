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
        public string? ImageDataUrl { get; set; }

        public ICollection<Vote> Votes { get; set; } = new List<Vote>();
    }
}
