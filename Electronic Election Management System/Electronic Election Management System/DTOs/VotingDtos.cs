using System.ComponentModel.DataAnnotations;

namespace Electronic_Election_Management_System.DTOs
{
    // SYNC: user-details.model.ts -> PersonalDetailsDto
    /// <summary>Shared DTO for vote declarations and user profiles.</summary>
    public class PersonalDetailsDto
    {
        // --- Politic ---
        public string? Cnp { get; set; }
        public string? FullName { get; set; }
        public string? ResidenceCounty { get; set; }
        public string? ResidenceAddress { get; set; }
        public string? ResidenceCity { get; set; }
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
        public List<Guid> OptionIds { get; set; } = new();

        /// <summary>Required for non-anonymous elections.</summary>
        public PersonalDetailsDto? VoterDeclaration { get; set; }
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
        public List<UserVoteAnswerDto> Answers { get; set; } = new();
    }

    public class UserVoteAnswerDto
    {
        public Guid QuestionId { get; set; }
        public Guid OptionId { get; set; }
        public string? OptionLabel { get; set; }
    }
}
