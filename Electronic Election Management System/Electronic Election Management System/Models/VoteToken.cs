using System;

namespace Electronic_Election_Management_System.Models
{
    public class VoteToken
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid UserId { get; set; }
        public User? User { get; set; }

        public Guid ElectionId { get; set; }
        public Election? Election { get; set; }

        /// <summary>Indicates whether the token has already been consumed by a vote. Once <c>true</c>, the token cannot be used again.</summary>
        public bool IsUsed { get; set; } = false;

        public DateTime IssuedAt { get; set; } = DateTime.UtcNow;

        public Vote? Vote { get; set; }
    }
}
