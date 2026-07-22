using System.ComponentModel.DataAnnotations;

namespace Electronic_Election_Management_System.DTOs
{
    // SYNC: voting.model.ts -> VoterDeclarationDto
    // Only the fields relevant to the election's Type need to be filled in by the client;
    // the server validates/derives the rest. See VoteService.BuildDeclaration.
    public class VoterDeclarationDto
    {
        // --- Politic ---
        public string? Cnp { get; set; }
        public string? FullName { get; set; }
        public string? DomiciliuJudet { get; set; }
        public string? DomiciliuAdresa { get; set; }
        public string? DomiciliuLocalitate { get; set; }
        public string? Citizenship { get; set; }

        // --- Comercial ---
        public string? Gender { get; set; }
        public string? WorkEmail { get; set; }
        public string? EmployeeId { get; set; }
        public string? Department { get; set; }
        public string? JobTitle { get; set; }
        public string? Company { get; set; }
    }

    // SYNC: voting.model.ts -> CastVoteRequest
    public class CastVoteRequest
    {
        [Required]
        public Guid ElectionId { get; set; }

        [Required]
        public Guid OptionId { get; set; }

        /// <summary>
        /// Required when the target election has IsAnonymous == false; ignored (never persisted)
        /// when the election is anonymous, since anonymous votes must not carry identity info.
        /// </summary>
        public VoterDeclarationDto? VoterDeclaration { get; set; }
    }

    // SYNC: voting.model.ts -> UserVoteDto
    public class UserVoteDto
    {
        public Guid ElectionId { get; set; }
        public Guid OptionId { get; set; }
        public string? OptionLabel { get; set; }
        public DateTime? VotedAt { get; set; }
        /// <summary>False once the voter has already used their one allowed change (edit or delete).</summary>
        public bool CanEdit { get; set; } = true;
    }
}
