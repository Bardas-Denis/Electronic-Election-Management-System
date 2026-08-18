using System;
using System.Collections.Generic;

namespace Electronic_Election_Management_System.Models
{
    public class ScoringScheme
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = string.Empty;
        
        /// <summary>
        /// Ordered list of points. Index 0 is 1st place, Index 1 is 2nd place, etc.
        /// </summary>
        public List<int> Points { get; set; } = new List<int>();
        
        /// <summary>
        /// If true, points are calculated dynamically based on the number of options (e.g. N down to 1),
        /// and the Points list is ignored.
        /// </summary>
        public bool IsLinear { get; set; }
        
        /// <summary>
        /// Predefined schemes cannot be edited or deleted by normal users.
        /// </summary>
        public bool IsPredefined { get; set; }
        
        public Guid? CreatedByUserId { get; set; }
        public User? CreatedByUser { get; set; }
    }
}
