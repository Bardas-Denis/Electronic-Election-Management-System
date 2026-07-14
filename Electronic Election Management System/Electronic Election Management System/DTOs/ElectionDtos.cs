using System.ComponentModel.DataAnnotations;

namespace Electronic_Election_Management_System.DTOs
{
    // SYNC: voting.model.ts -> OptionDto
    public class OptionDto
    {
        public Guid Id { get; set; } = Guid.Empty;
        public string Label { get; set; } = string.Empty;
        public string? Description { get; set; }
    }

    // SYNC: voting.model.ts -> OptionCreateDto
    public class CreateOptionDto
    {
        [Required]
        public string Label { get; set; } = string.Empty;
        public string? Description { get; set; }
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
        public DateTime StartsAt { get; set; }
        public DateTime EndsAt { get; set; }
        public List<OptionDto> Options { get; set; } = new();

        
        /// <summary>
        /// Indicates whether the current user has already cast a vote in this election.
        /// </summary>
        public bool HasUserVoted { get; set; } = false;
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

        [Required]
        public DateTime StartsAt { get; set; }

        [Required]
        /// <summary>The date and time when the election closes. Must be strictly after <see cref="StartsAt"/>.</summary>
        public DateTime EndsAt { get; set; }

        [Required, MinLength(2)]
        /// <summary>The options for this election. Must contain at least 2 items.</summary>
        public List<CreateOptionDto> Options { get; set; } = new();
    }

    // SYNC: voting.model.ts -> CreateElectionRequest (reused for PUT)
    public class UpdateElectionRequest : CreateElectionRequest
    {
    }
}
