using System;
using System.Collections.Generic;

namespace Electronic_Election_Management_System.DTOs
{
    // SYNC: results.model.ts -> OptionResultDto
    public class OptionResultDto
    {
        public Guid OptionId { get; set; }
        public string Label { get; set; } = string.Empty;
        public int VoteCount { get; set; }
        /// <summary>Fetch the picture itself from <c>GET /api/images/{id}</c>.</summary>
        public Guid? ImageId { get; set; }
        /// <summary>The synthetic "Other" entry: no real option behind it, so
        /// <see cref="OptionId"/> is empty and the frontend substitutes a translated label.</summary>
        public bool IsOtherOption { get; set; }
        public Dictionary<int, int>? RankCounts { get; set; }
    }

    public class QuestionResultDto
    {
        public Guid QuestionId { get; set; }
        public string Text { get; set; } = string.Empty;
        public bool AllowMultipleAnswers { get; set; }
        /// <summary>Valid values: <c>"Choice"</c> or <c>"FreeText"</c>.</summary>
        public string QuestionType { get; set; } = "Choice";
        // Single-answer: the sum of option counts. Multiple-answer and ranking: distinct
        // respondents, so several picks by one person do not inflate it - which is why an
        // option's own VoteCount can legitimately exceed this. FreeText: the answer count.
        public int TotalVotes { get; set; }
        public int? RequiredRankCount { get; set; }
        public ScoringSchemeDto? ScoringScheme { get; set; }
        public List<OptionResultDto> Results { get; set; } = new();
        /// <summary>FreeText questions only: the raw answers, with no attribution to who
        /// submitted them.</summary>
        public List<string> TextAnswers { get; set; } = new();
    }

    // SYNC: results.model.ts -> ElectionResultsDto
    public class ElectionResultsDto
    {
        public Guid ElectionId { get; set; }
        public string Title { get; set; } = string.Empty;
        public int TotalVotes { get; set; }
        public List<OptionResultDto> Results { get; set; } = new();
        public List<QuestionResultDto> Questions { get; set; } = new();
    }
}
