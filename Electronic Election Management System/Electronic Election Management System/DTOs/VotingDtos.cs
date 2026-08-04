using System.ComponentModel.DataAnnotations;
using Electronic_Election_Management_System.Constants;

namespace Electronic_Election_Management_System.DTOs
{
    // SYNC: user-details.model.ts -> PersonalDetailsDto
    /// <summary>Shared DTO for vote declarations and user profiles.</summary>
    public class PersonalDetailsDto
    {
        // --- Politic ---
        [RegularExpression(@"^\d{13}$")]
        public string? Cnp { get; set; }
        [StringLength(ValidationRules.ShortTextMaxLength)]
        public string? FullName { get; set; }
        [StringLength(ValidationRules.ShortTextMaxLength)]
        public string? ResidenceCounty { get; set; }
        [StringLength(ValidationRules.AddressMaxLength)]
        public string? ResidenceAddress { get; set; }
        [StringLength(ValidationRules.ShortTextMaxLength)]
        public string? ResidenceCity { get; set; }
        [StringLength(ValidationRules.ShortTextMaxLength)]
        public string? Citizenship { get; set; }

        // --- Comercial ---
        [RegularExpression(
            @"^(M|F|Male|Female|Other)$",
            ErrorMessage = ValidationMessages.InvalidGender)]
        public string? Gender { get; set; }
        [EmailAddress, StringLength(ValidationRules.EmailMaxLength)]
        public string? WorkEmail { get; set; }
        [StringLength(ValidationRules.ShortTextMaxLength)]
        public string? EmployeeId { get; set; }
        [StringLength(ValidationRules.ShortTextMaxLength)]
        public string? Department { get; set; }
        [StringLength(ValidationRules.ShortTextMaxLength)]
        public string? JobTitle { get; set; }
        [StringLength(ValidationRules.ShortTextMaxLength)]
        public string? Company { get; set; }
    }

    // SYNC: voting.model.ts -> CastVoteRequest
    public class CastVoteRequest : IValidatableObject
    {
        public Guid ElectionId { get; set; }

        public Guid OptionId { get; set; }
        [Required, MaxLength(ValidationRules.MaxQuestions)]
        public List<Guid> OptionIds { get; set; } = new();

        /// <summary>Required for non-anonymous elections.</summary>
        public PersonalDetailsDto? VoterDeclaration { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (ElectionId == Guid.Empty)
                yield return new ValidationResult(
                    ValidationMessages.ElectionIdRequired,
                    new[] { nameof(ElectionId) });

            // No blanket "at least one option overall" check here on purpose - a ballot made
            // entirely of optional questions can legitimately be submitted with zero picks.
            // Per-question requirements (including "required" ones) are enforced afterwards,
            // in VoteService.GetSelectedOptions, which knows about each question's own rules.

            if (OptionIds.Any(id => id == Guid.Empty) || OptionIds.Count != OptionIds.Distinct().Count())
                yield return new ValidationResult(
                    ValidationMessages.InvalidOptionIds,
                    new[] { nameof(OptionIds) });
        }
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
