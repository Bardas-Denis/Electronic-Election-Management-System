namespace Electronic_Election_Management_System.Models
{
    public enum QuestionType
    {
        Choice,
        FreeText,
        Ranking
    }

    public class ElectionQuestion
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid ElectionId { get; set; }
        public Election? Election { get; set; }
        public string Text { get; set; } = string.Empty;
        public string? ImageDataUrl { get; set; }
        public int DisplayOrder { get; set; }
        public bool IsRequired { get; set; } = true;
        public bool AllowMultipleAnswers { get; set; } = false;
        public QuestionType QuestionType { get; set; } = QuestionType.Choice;
        /// <summary>Only meaningful for a <see cref="Models.QuestionType.Choice"/> question: when
        /// true, voters may answer with free text ("Other: ___") instead of / alongside picking
        /// one of the fixed options, stored the same way as a FreeText answer.</summary>
        public bool AllowOtherOption { get; set; } = false;
        /// <summary>Only meaningful for a <see cref="Models.QuestionType.Ranking"/> question: when
        /// set, a ballot must place exactly this many options, so every voter spreads the same
        /// number of points and the scores stay comparable. Null leaves the count open - a voter
        /// ranks as few or as many as they like.</summary>
        public int? RequiredRankCount { get; set; }
        /// <summary>For <see cref="Models.QuestionType.Choice"/> questions, the selectable options.
        /// For <see cref="Models.QuestionType.FreeText"/> questions, optional suggestion chips shown
        /// above the text box - not a restrictive list, voters may type anything.</summary>
        public ICollection<Option> Options { get; set; } = new List<Option>();
        public ICollection<Vote> Votes { get; set; } = new List<Vote>();
    }
}
