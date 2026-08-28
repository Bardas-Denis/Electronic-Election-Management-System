using System;

namespace Electronic_Election_Management_System.Models
{
    public class Vote
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>Set for an answer to a <see cref="QuestionType.Choice"/> question; exactly one of
        /// <see cref="OptionId"/> and (<see cref="QuestionId"/> + <see cref="AnswerText"/>) is set.</summary>
        public Guid? OptionId { get; set; }
        public Option? Option { get; set; }

        /// <summary>Set for an answer to a <see cref="QuestionType.FreeText"/> question, alongside
        /// <see cref="AnswerText"/>, instead of <see cref="OptionId"/>.</summary>
        public Guid? QuestionId { get; set; }
        public ElectionQuestion? Question { get; set; }
        /// <summary>The voter's typed answer for a <see cref="QuestionType.FreeText"/> question.</summary>
        public string? AnswerText { get; set; }

        /// <summary>Used when the question is of type <see cref="QuestionType.Ranking"/>. Indicates the preference rank (1 is highest).</summary>
        public int? Rank { get; set; }

        /// <summary>Set when the parent election is anonymous; exactly one of <see cref="VoteTokenId"/> and <see cref="UserId"/> is non-null.</summary>
        public Guid? VoteTokenId { get; set; }
        public VoteToken? VoteToken { get; set; }
        /// <summary>Set when the parent election is non-anonymous; exactly one of <see cref="UserId"/> and <see cref="VoteTokenId"/> is non-null.</summary>
        public Guid? UserId { get; set; }
        public User? User { get; set; }

        public DateTime CastAt { get; set; } = DateTime.UtcNow;

        /// <summary>Present only when the parent election is non-anonymous.</summary>
        public VoterDeclaration? VoterDeclaration { get; set; }
    }
}
