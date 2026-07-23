using System.ComponentModel.DataAnnotations;

namespace Electronic_Election_Management_System.DTOs
{
    // SYNC: voting.model.ts -> OptionDto
    public class OptionDto
    {
        public Guid Id { get; set; } = Guid.Empty;
        public string Label { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? ImageDataUrl { get; set; }
    }

    // SYNC: voting.model.ts -> OptionCreateDto
    public class CreateOptionDto
    {
        [Required]
        public string Label { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? ImageDataUrl { get; set; }
    }

    public class ElectionQuestionDto
    {
        public Guid Id { get; set; }
        public string Text { get; set; } = string.Empty;
        public int DisplayOrder { get; set; }
        public List<OptionDto> Options { get; set; } = new();
    }

    public class CreateElectionQuestionDto
    {
        [Required]
        public string Text { get; set; } = string.Empty;
        [Required, MinLength(2)]
        public List<CreateOptionDto> Options { get; set; } = new();
    }

    // SYNC: voting.model.ts -> ElectionDto
    public class ElectionDto
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        /// <summary>The actual question presented to voters, shown above the options.</summary>
        public string? Question { get; set; }
        /// <summary>The election category. Valid values: <c>"Politic"</c> or <c>"Comercial"</c>.</summary>
        public string Type { get; set; } = string.Empty;
        public bool IsAnonymous { get; set; }
        public bool IsClosed { get; set; }
        public DateTime StartsAt { get; set; }
        public DateTime EndsAt { get; set; }
        public List<OptionDto> Options { get; set; } = new();
        public List<ElectionQuestionDto> Questions { get; set; } = new();

        
        /// <summary>
        /// Indicates whether the current user has already cast a vote in this election.
        /// </summary>
        public bool HasUserVoted { get; set; } = false;

        /// <summary>
        /// True once <see cref="EndsAt"/> has passed. Expired elections no longer accept new votes,
        /// but their previously registered votes and results remain fully accessible.
        /// </summary>
        public bool IsExpired { get; set; } = false;

        /// <summary>
        /// True once at least one vote has been cast in this election (by anyone). Once true, the
        /// election can no longer be edited — only viewed, results checked, or deleted.
        /// </summary>
        public bool HasVotes { get; set; } = false;
    }

    // SYNC: voting.model.ts -> CreateElectionRequest
    public class CreateElectionRequest
    {
        [Required]
        public string Title { get; set; } = string.Empty;

        public string? Description { get; set; }

        [Required]
        /// <summary>The actual question presented to voters, shown above the options.</summary>
        public string Question { get; set; } = string.Empty;

        [Required]
        /// <summary>The election category. Valid values: <c>"Politic"</c> or <c>"Comercial"</c>.</summary>
        public string Type { get; set; } = string.Empty;

        public bool IsAnonymous { get; set; } = true;

        /// <summary>When true, only the creator and invited users can discover or access the election.</summary>
        public bool IsClosed { get; set; }

        /// <summary>Existing accounts to invite directly when the closed election is created.</summary>
        public List<Guid> InvitedUserIds { get; set; } = new();

        /// <summary>Email addresses to invite, including addresses that have not registered yet.</summary>
        public List<string> InvitedEmails { get; set; } = new();

        [Required]
        public DateTime StartsAt { get; set; }

        [Required]
        /// <summary>The date and time when the election closes. Must be strictly after <see cref="StartsAt"/>.</summary>
        public DateTime EndsAt { get; set; }

        [Required, MinLength(2)]
        /// <summary>The options for this election. Must contain at least 2 items.</summary>
        public List<CreateOptionDto> Options { get; set; } = new();
        public List<CreateElectionQuestionDto> Questions { get; set; } = new();
    }

    // SYNC: voting.model.ts -> CreateElectionRequest (reused for PUT).
    // Invitation collections are used during creation only; later invitation
    // changes go through the dedicated invitations endpoints.
    public class UpdateElectionRequest : CreateElectionRequest
    {
    }

    public class InviteToElectionRequest
    {
        public List<Guid> UserIds { get; set; } = new();
        public List<string> Emails { get; set; } = new();
    }

    public class ElectionInvitationDto
    {
        public Guid Id { get; set; }
        public Guid? UserId { get; set; }
        public string Email { get; set; } = string.Empty;
        public string Method { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }

    /// <summary>Minimal account information exposed to election creators for manual invitations.</summary>
    public class InvitationCandidateDto
    {
        public Guid Id { get; set; }
        public string Email { get; set; } = string.Empty;
    }
}
