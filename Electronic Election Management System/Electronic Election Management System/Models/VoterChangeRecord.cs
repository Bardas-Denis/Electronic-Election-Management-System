using System;

namespace Electronic_Election_Management_System.Models
{
    /// <summary>
    /// Tracks how many times a given user has changed their answer in a given election, where
    /// "changed" means either editing the vote (PUT) or deleting it (DELETE) - both consume the
    /// same one-time budget. This is intentionally separate from <see cref="Vote"/> because the
    /// Vote row is deleted when a user deletes their answer; if the limit lived on the Vote row
    /// it would reset to zero on every delete, letting someone bypass it via delete+revote loops.
    /// One row per (UserId, ElectionId), created the first time a change is made and never removed.
    /// </summary>
    public class VoterChangeRecord
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid UserId { get; set; }
        public User? User { get; set; }

        public Guid ElectionId { get; set; }
        public Election? Election { get; set; }

        /// <summary>Number of edit/delete changes made so far. Currently capped at 1.</summary>
        public int ChangeCount { get; set; } = 0;
    }
}
