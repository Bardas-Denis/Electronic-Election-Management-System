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

    /// <summary>One voter who picked a given option. Only ever returned for a non-anonymous
    /// election, and only to an Admin or the election's creator - see
    /// <c>ResultsService.GetOptionVotersAsync</c>. Deliberately kept out of
    /// <see cref="ElectionResultsDto"/>, which is broadcast over SignalR to every subscriber
    /// in the election group.</summary>
    public class OptionVoterDto
    {
        public Guid UserId { get; set; }
        public string Email { get; set; } = string.Empty;
        /// <summary>The name declared for this vote if there is one, otherwise the name on the
        /// account, otherwise null - a voter who never filled in a profile has neither. The email
        /// is always sent alongside because names are not unique and this is an accountability
        /// view: two people called the same thing have to stay tellable apart.</summary>
        public string? FullName { get; set; }
    }

    /// <summary>The voters behind one option, grouped so that a single request can answer both
    /// "who voted for everything" and "who voted for this one".</summary>
    public class OptionVotersDto
    {
        public Guid OptionId { get; set; }
        public string Label { get; set; } = string.Empty;
        public List<OptionVoterDto> Voters { get; set; } = new();
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
        /// <summary>A property of the election rather than of whoever is looking, so it stays
        /// correct in the SignalR broadcast too. The dashboard uses it to decide whether asking
        /// who voted for what is even a possibility.</summary>
        public bool IsAnonymous { get; set; }
        public int TotalVotes { get; set; }
        public List<OptionResultDto> Results { get; set; } = new();
        public List<QuestionResultDto> Questions { get; set; } = new();
    }
}
