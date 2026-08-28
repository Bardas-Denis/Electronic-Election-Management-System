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

        /// <summary>Superseded by <see cref="ImageId"/>. Kept only until the backfill has run
        /// on every environment; the column is dropped by a follow-up migration.</summary>
        public string? ImageDataUrl { get; set; }

        /// <summary>The question's illustration, stored out-of-row in <see cref="ElectionImage"/>.</summary>
        public Guid? ImageId { get; set; }
        public ElectionImage? Image { get; set; }

        public int DisplayOrder { get; set; }
        public bool IsRequired { get; set; } = true;
        public bool AllowMultipleAnswers { get; set; } = false;
        public QuestionType QuestionType { get; set; } = QuestionType.Choice;
        /// <summary>Only meaningful for a <see cref="Models.QuestionType.Choice"/> question: when
        /// true, voters may answer with free text ("Other: ___") instead of / alongside picking
        /// one of the fixed options, stored the same way as a FreeText answer.</summary>
        public bool AllowOtherOption { get; set; } = false;
        /// <summary>Ranking questions only. Forcing the same count on every ballot keeps the
        /// point spread comparable across voters. Null leaves it open.</summary>
        public int? RequiredRankCount { get; set; }
        public Guid? ScoringSchemeId { get; set; }
        public ScoringScheme? ScoringScheme { get; set; }
        /// <summary>Selectable options for a Choice question. For FreeText, optional suggestion
        /// chips - voters may still type anything.</summary>
        public ICollection<Option> Options { get; set; } = new List<Option>();
        public ICollection<Vote> Votes { get; set; } = new List<Vote>();
    }
}
