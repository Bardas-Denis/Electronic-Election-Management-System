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

        /// <summary>Valid values: <c>"None"</c>, <c>"County"</c>, <c>"Region"</c>, <c>"Custom"</c>.
        /// "Custom" reads its group definitions from <see cref="CustomRegionGroupsJson"/>, applied
        /// to the raw field chosen in <see cref="CustomGroupingBaseField"/>.</summary>
        public string RegionalGroupingType { get; set; } = "None";

        /// <summary>
        /// Only meaningful when <see cref="RegionalGroupingType"/> is <c>"Custom"</c>. Which raw
        /// voter field the creator's custom groups are built from - valid values:
        /// <c>"County"</c> (domiciliu/județ - for grouping within one country, e.g. Romanian
        /// regions), <c>"City"</c>, or <c>"Citizenship"</c> (nationality/country - for grouping
        /// across countries, e.g. hemispheres or continents on a global election). Defaults to
        /// "County" for elections created before this field existed.
        /// </summary>
        public string CustomGroupingBaseField { get; set; } = "County";

        /// <summary>
        /// JSON snapshot of the creator-defined region groups (only meaningful when
        /// <see cref="RegionalGroupingType"/> is <c>"Custom"</c>), e.g. mapping raw counties
        /// like "Cluj" / "Sălaj" into a creator-named group like "Nord-Vest", or - when
        /// <see cref="CustomGroupingBaseField"/> is "Citizenship" - mapping countries like
        /// "Romania" / "Germany" into a group like "Eastern Hemisphere". Stored the same way as
        /// <see cref="AudienceGroupsSnapshot"/> - a JSON snapshot on the election itself, rather
        /// than a separate table, since the grouping is specific to this one election and never
        /// queried on its own.
        /// </summary>
        public string? CustomRegionGroupsJson { get; set; }

        /// <summary>
        /// When <c>true</c>, votes are recorded via <see cref="VoteToken"/> with no user link (Vote.VoteTokenId is set, Vote.UserId is null).
        /// When <c>false</c>, Vote.UserId is set directly and Vote.VoteTokenId is null.
        /// </summary>
        public bool IsAnonymous { get; set; } = true;

        /// <summary>
        /// Closed elections are visible only to their creator and explicitly invited users.
        /// </summary>
        public bool IsClosed { get; set; }

        /// <summary>
        /// When <c>false</c>, the election is hidden from voters regardless of its scheduled
        /// date window. The owner must explicitly "Start" (publish) it to make it visible.
        /// Defaults to <c>true</c> so elections created without touching this flag behave as before.
        /// </summary>
        public bool IsVisible { get; set; } = true;

        /// <summary>
        /// JSON snapshot of the audience group rules used when creating a closed election,
        /// so edit mode can restore and display user groupings.
        /// </summary>
        public string? AudienceGroupsSnapshot { get; set; }

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
            if (!IsVisible)
                return false;
            var now = DateTime.UtcNow;
            return now >= StartsAt && now <= EndsAt;
        }
    }
}