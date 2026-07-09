using System;

namespace Electronic_Election_Management_System.Models
{
    public class Vote
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid OptionId { get; set; }
        public Option? Option { get; set; }

        /// <summary>Set when the parent election is anonymous; exactly one of <see cref="VoteTokenId"/> and <see cref="UserId"/> is non-null.</summary>
        public Guid? VoteTokenId { get; set; }
        public VoteToken? VoteToken { get; set; }
        /// <summary>Set when the parent election is non-anonymous; exactly one of <see cref="UserId"/> and <see cref="VoteTokenId"/> is non-null.</summary>
        public Guid? UserId { get; set; }
        public User? User { get; set; }

        public DateTime CastAt { get; set; } = DateTime.UtcNow;
    }
}
