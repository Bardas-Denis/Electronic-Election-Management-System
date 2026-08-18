using System.ComponentModel.DataAnnotations;

namespace Electronic_Election_Management_System.DTOs
{
    /// <summary>Shared API input limits. The Angular forms mirror these values.</summary>
    public static class ValidationRules
    {
        public const int EmailMaxLength = 254;
        public const int PasswordMaxLength = 128;
        public const int ShortTextMaxLength = 100;
        public const int LabelNameMaxLength = 50;
        public const int LabelCategoryMaxLength = 50;
        public const int TitleMaxLength = 200;
        public const int QuestionMaxLength = 500;
        public const int DescriptionMaxLength = 2_000;
        // FreeText questions allow a longer answer than a Choice question's "Other" answer.
        public const int AnswerMaxLength = 150;
        public const int OtherAnswerMaxLength = 50;
        public const int AddressMaxLength = 500;
        public const int ImageDataUrlMaxLength = 3_000_000;
        public const int MaxQuestions = 20;
        public const int MaxOptionsPerQuestion = 50;
        public const int MaxInvitations = 500;
        /// <summary>Maximum number of label conditions inside one AND-group.</summary>
        public const int MaxLabelsPerGroup = 10;
        /// <summary>Maximum number of AND-groups in a single audience rule.</summary>
        public const int MaxAudienceGroups = 20;
    }

    /// <summary>Rejects strings that contain only whitespace.</summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
    public sealed class NotWhitespaceAttribute : ValidationAttribute
    {
        public override bool IsValid(object? value)
            => value is null || value is not string text || !string.IsNullOrWhiteSpace(text);
    }
}
