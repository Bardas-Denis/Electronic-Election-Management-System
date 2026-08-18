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
        public string? ImageDataUrl { get; set; }
        /// <summary>True for the synthetic entry representing a Choice question's "Other"
        /// answers - there is no real <see cref="Models.Option"/> behind it, so
        /// <see cref="OptionId"/> is <see cref="Guid.Empty"/> and the frontend substitutes a
        /// translated label instead of <see cref="Label"/>.</summary>
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
        // For a single-answer question this is the sum of option vote counts (every respondent
        // picked exactly one). For a multiple-answer question it's the distinct respondent count
        // instead, since a respondent's several picks would otherwise inflate this beyond the
        // number of people who actually answered - each option's own VoteCount can then exceed
        // this total, and that's expected. For a FreeText question it's simply the number of
        // submitted answers.
        public int TotalVotes { get; set; }
        public int? RequiredRankCount { get; set; }
        public ScoringSchemeDto? ScoringScheme { get; set; }
        public List<OptionResultDto> Results { get; set; } = new();
        /// <summary>Populated only for a <c>"FreeText"</c> question - the raw submitted answers,
        /// with no attribution to who submitted them (consistent with option tallies never
        /// exposing individual votes either).</summary>
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
