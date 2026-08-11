using System.ComponentModel.DataAnnotations;
using Electronic_Election_Management_System.Constants;

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
        [Required, NotWhitespace, StringLength(ValidationRules.ShortTextMaxLength)]
        public string Label { get; set; } = string.Empty;
        [StringLength(ValidationRules.DescriptionMaxLength)]
        public string? Description { get; set; }
        [StringLength(ValidationRules.ImageDataUrlMaxLength)]
        public string? ImageDataUrl { get; set; }
    }

    public class ElectionQuestionDto
    {
        public Guid Id { get; set; }
        public string Text { get; set; } = string.Empty;
        public int DisplayOrder { get; set; }
        public bool IsRequired { get; set; } = true;
        public bool AllowMultipleAnswers { get; set; } = false;
        /// <summary>Valid values: <c>"Choice"</c> or <c>"FreeText"</c>.</summary>
        public string QuestionType { get; set; } = "Choice";
        /// <summary>Only meaningful for a <c>"Choice"</c> question: when true, voters may answer
        /// with free text ("Other: ___") instead of / alongside picking a fixed option.</summary>
        public bool AllowOtherOption { get; set; } = false;
        /// <summary>Only meaningful for a <c>"Ranking"</c> question: when set, a ballot must place
        /// exactly this many options. Null leaves the count open.</summary>
        public int? RequiredRankCount { get; set; }
        /// <summary>For a <c>"Choice"</c> question, the selectable options. For a <c>"FreeText"</c>
        /// question, optional suggestion chips - voters may still type anything.</summary>
        public List<OptionDto> Options { get; set; } = new();
    }

    public class CreateElectionQuestionDto : IValidatableObject
    {
        [Required, NotWhitespace, StringLength(ValidationRules.QuestionMaxLength)]
        public string Text { get; set; } = string.Empty;
        public bool IsRequired { get; set; } = true;
        public bool AllowMultipleAnswers { get; set; } = false;
        /// <summary>Valid values: <c>"Choice"</c> or <c>"FreeText"</c>.</summary>
        [Required, NotWhitespace, StringLength(20)]
        public string QuestionType { get; set; } = "Choice";
        /// <summary>Only meaningful for a <c>"Choice"</c> question: when true, voters may answer
        /// with free text ("Other: ___") instead of / alongside picking a fixed option.</summary>
        public bool AllowOtherOption { get; set; } = false;
        /// <summary>Only meaningful for a <c>"Ranking"</c> question: when set, a ballot must place
        /// exactly this many options. Must be between 1 and the option count (enforced in
        /// <c>ElectionService.QuestionsAreValid</c>). Null leaves the count open.</summary>
        public int? RequiredRankCount { get; set; }
        /// <summary>Required to have at least 2 for a <c>"Choice"</c> question (enforced in
        /// <c>ElectionService.QuestionsAreValid</c>, since the requirement depends on
        /// <see cref="QuestionType"/>); optional suggestion chips for a <c>"FreeText"</c> question.</summary>
        [MaxLength(ValidationRules.MaxOptionsPerQuestion)]
        public List<CreateOptionDto> Options { get; set; } = new();

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            var labels = Options
                .Where(option => !string.IsNullOrWhiteSpace(option.Label))
                .Select(option => option.Label.Trim())
                .ToList();
            if (labels.Count != labels.Distinct(StringComparer.OrdinalIgnoreCase).Count())
                yield return new ValidationResult(ValidationMessages.DuplicateOptionLabels, new[] { nameof(Options) });
        }
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
        /// <summary>
        /// False while the owner has not yet published ("started") the election.
        /// Invisible elections are hidden from voters' listing and cannot accept votes.
        /// </summary>
        public bool IsVisible { get; set; } = true;
        public DateTime StartsAt { get; set; }
        public DateTime EndsAt { get; set; }
        public List<OptionDto> Options { get; set; } = new();
        public List<ElectionQuestionDto> Questions { get; set; } = new();

        /// <summary>
        /// Audience group rules saved when creating a closed election.
        /// Restored in edit mode to preserve group badges and summaries.
        /// </summary>
        public List<AudienceGroupDto>? AudienceGroups { get; set; }

        
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

    // SYNC: voting.model.ts -> AudienceConditionDto
    public class AudienceConditionDto
    {
        public Guid LabelId { get; set; }
        /// <summary>True means "does NOT have this label" (NOT condition).</summary>
        public bool IsExcluded { get; set; } = false;
    }

    // SYNC: voting.model.ts -> AudienceGroupDto
    public class AudienceGroupDto
    {
        [Required, MinLength(1), MaxLength(ValidationRules.MaxLabelsPerGroup)]
        public List<AudienceConditionDto> Conditions { get; set; } = new();
    }

    // SYNC: voting.model.ts -> CreateElectionRequest
    public class CreateElectionRequest : IValidatableObject
    {
        [Required, NotWhitespace, StringLength(ValidationRules.TitleMaxLength)]
        public string Title { get; set; } = string.Empty;

        [StringLength(ValidationRules.DescriptionMaxLength)]
        public string? Description { get; set; }

        /// <summary>The actual question presented to voters, shown above the options.</summary>
        [Required, NotWhitespace, StringLength(ValidationRules.QuestionMaxLength)]
        public string Question { get; set; } = string.Empty;

        /// <summary>The election category. Valid values: <c>"Politic"</c> or <c>"Comercial"</c>.</summary>
        [Required, NotWhitespace, StringLength(20)]
        public string Type { get; set; } = string.Empty;

        public bool IsAnonymous { get; set; } = true;

        /// <summary>When true, only the creator and invited users can discover or access the election.</summary>
        public bool IsClosed { get; set; }

        /// <summary>
        /// When false, the election is hidden from voters until the owner explicitly calls the Start (publish) action.
        /// Defaults to true so creating an election without specifying this field makes it immediately visible.
        /// </summary>
        public bool IsVisible { get; set; } = true;

        /// <summary>Existing accounts to invite directly when the closed election is created.</summary>
        [Required, MaxLength(ValidationRules.MaxInvitations)]
        public List<Guid> InvitedUserIds { get; set; } = new();

        /// <summary>Email addresses to invite, including addresses that have not registered yet.</summary>
        [Required, MaxLength(ValidationRules.MaxInvitations)]
        public List<string> InvitedEmails { get; set; } = new();

        /// <summary>
        /// Audience rule: an OR of AND-groups. Each group's conditions are ANDed together;
        /// IsExcluded conditions apply a NOT. Expanded into individual invitations at creation time.
        /// </summary>
        [Required, MaxLength(ValidationRules.MaxAudienceGroups)]
        public List<AudienceGroupDto> InvitedAudienceGroups { get; set; } = new();

        public DateTime StartsAt { get; set; }

        /// <summary>The date and time when the election closes. Must be strictly after <see cref="StartsAt"/>.</summary>
        public DateTime EndsAt { get; set; }

        /// <summary>The options for this election, used only when <see cref="Questions"/> is
        /// empty (legacy single-question elections). Minimum-count requirements depend on
        /// question type and are enforced in <c>ElectionService.QuestionsAreValid</c>.</summary>
        [Required, MaxLength(ValidationRules.MaxOptionsPerQuestion)]
        public List<CreateOptionDto> Options { get; set; } = new();
        [Required, MaxLength(ValidationRules.MaxQuestions)]
        public List<CreateElectionQuestionDto> Questions { get; set; } = new();

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (StartsAt == default)
                yield return new ValidationResult(ValidationMessages.StartDateRequired, new[] { nameof(StartsAt) });
            if (EndsAt == default)
                yield return new ValidationResult(ValidationMessages.EndDateRequired, new[] { nameof(EndsAt) });
            if (StartsAt != default && EndsAt != default && EndsAt <= StartsAt)
                yield return new ValidationResult(ValidationMessages.InvalidDateRange, new[] { nameof(EndsAt) });

            if (string.Equals(Type?.Trim(), "Politic", StringComparison.OrdinalIgnoreCase) &&
                IsAnonymous)
            {
                yield return new ValidationResult(
                    ValidationMessages.PoliticalElectionCannotBeAnonymous,
                    new[] { nameof(IsAnonymous) });
            }

            // At least one question must be required, so an election can never end up with
            // nothing mandatory to answer.
            if (Questions.Count > 0 && Questions.All(question => !question.IsRequired))
                yield return new ValidationResult(
                    ValidationMessages.AtLeastOneRequiredQuestion,
                    new[] { nameof(Questions) });

            if (InvitedUserIds.Any(id => id == Guid.Empty))
                yield return new ValidationResult(
                    ValidationMessages.InvalidInvitedUserIds,
                    new[] { nameof(InvitedUserIds) });

            var emailValidator = new EmailAddressAttribute();
            if (InvitedEmails.Any(email =>
                    string.IsNullOrWhiteSpace(email) ||
                    email.Length > ValidationRules.EmailMaxLength ||
                    !emailValidator.IsValid(email)))
            {
                yield return new ValidationResult(
                    ValidationMessages.InvalidInvitationEmails,
                    new[] { nameof(InvitedEmails) });
            }

            // Audience group validation: mirrors the InvitedUserIds checks above.
            if (InvitedAudienceGroups.Any(group =>
                    group.Conditions.Any(c => c.LabelId == Guid.Empty)))
            {
                yield return new ValidationResult(
                    ValidationMessages.AudienceConditionLabelIdInvalid,
                    new[] { nameof(InvitedAudienceGroups) });
            }

            if (InvitedAudienceGroups.Any(group =>
                    group.Conditions.All(c => c.IsExcluded)))
            {
                yield return new ValidationResult(
                    ValidationMessages.AudienceGroupRequiresPositiveCondition,
                    new[] { nameof(InvitedAudienceGroups) });
            }
        }
    }

    // SYNC: voting.model.ts -> CreateElectionRequest (reused for PUT).
    // Invitation collections are used during creation only; later invitation
    // changes go through the dedicated invitations endpoints.
    public class UpdateElectionRequest : CreateElectionRequest
    {
    }

    public class InviteToElectionRequest : IValidatableObject
    {
        [Required, MaxLength(ValidationRules.MaxInvitations)]
        public List<Guid> UserIds { get; set; } = new();
        [Required, MaxLength(ValidationRules.MaxInvitations)]
        public List<string> Emails { get; set; } = new();

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (UserIds.Any(id => id == Guid.Empty))
                yield return new ValidationResult(ValidationMessages.InvalidUserIds, new[] { nameof(UserIds) });

            var emailValidator = new EmailAddressAttribute();
            if (Emails.Any(email =>
                    string.IsNullOrWhiteSpace(email) ||
                    email.Length > ValidationRules.EmailMaxLength ||
                    !emailValidator.IsValid(email)))
            {
                yield return new ValidationResult(
                    ValidationMessages.InvalidInvitationEmails,
                    new[] { nameof(Emails) });
            }

            if (UserIds.Count == 0 && Emails.Count == 0)
                yield return new ValidationResult(ValidationMessages.InvitationRecipientRequired);
        }
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

    /// <summary>A label available as an audience source for a closed election.</summary>
    public class InvitationLabelDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Category { get; set; }
        public int UserCount { get; set; }
        /// <summary>IDs of users assigned to this label (excluding the requesting user).</summary>
        public List<Guid> UserIds { get; set; } = [];
    }
}
