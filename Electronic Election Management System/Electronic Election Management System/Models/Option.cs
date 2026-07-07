using System;
using System.Collections.Generic;

namespace Electronic_Election_Management_System.Models
{
    // Reprezinta o varianta de vot in cadrul unei alegeri (ex: un candidat, un raspuns de sondaj)
    public class Option
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid ElectionId { get; set; }
        public Election? Election { get; set; }

        public string Label { get; set; } = string.Empty;

        // Navigatie
        public ICollection<Vote> Votes { get; set; } = new List<Vote>();
    }
}
