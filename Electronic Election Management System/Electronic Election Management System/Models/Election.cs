using System;
using System.Collections.Generic;

namespace Electronic_Election_Management_System.Models
{
    public enum ElectionType
    {
        Politic,
        Comercial
    }

    public class Election
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid CreatedByUserId { get; set; }
        public User? CreatedByUser { get; set; }

        public string Title { get; set; } = string.Empty;

        public string? Description { get; set; }

        /// <summary>The actual question presented to voters, shown above the options on the voting screen.</summary>
        public string? Question { get; set; }

        public ElectionType Type { get; set; }

        /// <summary>
        /// When <c>true</c>, votes are recorded via <see cref="VoteToken"/> with no user link (Vote.VoteTokenId is set, Vote.UserId is null).
        /// When <c>false</c>, Vote.UserId is set directly and Vote.VoteTokenId is null.
        /// </summary>
        public bool IsAnonymous { get; set; } = true;

        /// <summary>
        /// Closed elections are visible only to their creator and explicitly invited users.
        /// </summary>
        public bool IsClosed { get; set; }

        public DateTime StartsAt { get; set; }
        public DateTime EndsAt { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<Option> Options { get; set; } = new List<Option>();
        public ICollection<ElectionQuestion> Questions { get; set; } = new List<ElectionQuestion>();
        public ICollection<VoteToken> VoteTokens { get; set; } = new List<VoteToken>();
        public ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();
        public ICollection<ElectionInvitation> Invitations { get; set; } = new List<ElectionInvitation>();

        public bool CanAcceptVotes()
        {
            var now = DateTime.UtcNow;
            return now >= StartsAt && now <= EndsAt;
        }
    }
}
