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
    }

    public class QuestionResultDto
    {
        public Guid QuestionId { get; set; }
        public string Text { get; set; } = string.Empty;
        public int TotalVotes { get; set; }
        public List<OptionResultDto> Results { get; set; } = new();
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
