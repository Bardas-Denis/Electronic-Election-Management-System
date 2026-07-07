using System;

namespace Electronic_Election_Management_System.Models
{
    // Regula de anonimat (impusa si la nivel de baza de date prin CHECK constraint):
    //   - Alegere ANONIMA   -> VoteTokenId completat, UserId = null
    //   - Alegere NEANONIMA -> UserId completat,      VoteTokenId = null
    // Niciodata ambele, niciodata niciuna.
    public class Vote
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid OptionId { get; set; }
        public Option? Option { get; set; }

        // --- Flux ANONIM ---
        public Guid? VoteTokenId { get; set; }
        public VoteToken? VoteToken { get; set; }

        // --- Flux NEANONIM ---
        public Guid? UserId { get; set; }
        public User? User { get; set; }

        public DateTime CastAt { get; set; } = DateTime.UtcNow;
    }
}
