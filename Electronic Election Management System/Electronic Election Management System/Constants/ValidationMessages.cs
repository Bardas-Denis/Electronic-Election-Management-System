namespace Electronic_Election_Management_System.Constants
{
    /// <summary>Centralized messages returned when API request validation fails.</summary>
    public static class ValidationMessages
    {
        public const string InvalidGender = "Gender is invalid.";
        public const string ElectionIdRequired = "Election ID is required.";
        public const string OptionSelectionRequired = "At least one option is required.";
        public const string InvalidOptionIds = "Option IDs must be non-empty and unique.";
        public const string DuplicateOptionLabels = "Option labels must be unique within a question.";
        public const string StartDateRequired = "Start date is required.";
        public const string EndDateRequired = "End date is required.";
        public const string InvalidDateRange = "End date must be after start date.";
        public const string PoliticalElectionCannotBeAnonymous = "Political elections cannot be anonymous.";
        public const string InvalidInvitedUserIds = "Invited user IDs cannot be empty.";
        public const string InvalidInvitationEmails = "One or more invitation emails are invalid.";
        public const string InvalidUserIds = "User IDs cannot be empty.";
        public const string InvitationRecipientRequired = "At least one invitation recipient is required.";
        public const string AtLeastOneRequiredQuestion = "At least one question must be required.";
        public const string InvalidTextAnswers = "Text answers must reference distinct, valid questions.";
    }
}
