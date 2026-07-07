using System;

namespace Electronic_Election_Management_System.Models
{
    // Folosit DOAR pentru alegerile anonime.
    // Se emite o singura data per (User, Election). Se consuma la vot.
    // Votes.VoteTokenId trimite aici, dar NU exista drum invers de la Vote la User.
    public class VoteToken
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid UserId { get; set; }
        public User? User { get; set; }

        public Guid ElectionId { get; set; }
        public Election? Election { get; set; }

        public bool IsUsed { get; set; } = false;

        public DateTime IssuedAt { get; set; } = DateTime.UtcNow;

        // Navigatie (0 sau 1 vot pe token)
        public Vote? Vote { get; set; }
    }
}
