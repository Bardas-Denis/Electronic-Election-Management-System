using Microsoft.EntityFrameworkCore;
using Electronic_Election_Management_System.Models;

namespace Electronic_Election_Management_System.Data
{
    public class ElectionDbContext : DbContext
    {
        public ElectionDbContext(DbContextOptions<ElectionDbContext> options)
            : base(options) { }

        public DbSet<User> Users => Set<User>();
        public DbSet<Election> Elections => Set<Election>();
        public DbSet<Option> Options => Set<Option>();
        public DbSet<VoteToken> VoteTokens => Set<VoteToken>();
        public DbSet<Vote> Votes => Set<Vote>();
        public DbSet<VoterDeclaration> VoterDeclarations => Set<VoterDeclaration>();
        public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
        public DbSet<VoterChangeRecord> VoterChangeRecords => Set<VoterChangeRecord>();
        public DbSet<ElectionInvitation> ElectionInvitations => Set<ElectionInvitation>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<User>()
                .HasIndex(u => u.Email)
                .IsUnique();

            // Stored as string ("Admin"/"ElectionManager"/"Voter").
            // Explicit lambdas are used instead of HasConversion<string>() so that
            // EF Core does NOT generate a cached switch-case expression that would
            // break when new enum values are added without a full clean rebuild.
            modelBuilder.Entity<User>()
                .Property(u => u.Role)
                .HasConversion(
                    role => role.ToString(),
                    value => (UserRole)Enum.Parse(typeof(UserRole), value));

            modelBuilder.Entity<Election>()
                .Property(e => e.Type)
                .HasConversion<string>();

            modelBuilder.Entity<ElectionInvitation>()
                .Property(i => i.Method)
                .HasConversion<string>();

            modelBuilder.Entity<ElectionInvitation>()
                .HasIndex(i => new { i.ElectionId, i.Email })
                .IsUnique();

            modelBuilder.Entity<ElectionInvitation>()
                .HasOne(i => i.Election)
                .WithMany(e => e.Invitations)
                .HasForeignKey(i => i.ElectionId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ElectionInvitation>()
                .HasOne(i => i.User)
                .WithMany(u => u.ElectionInvitations)
                .HasForeignKey(i => i.UserId)
                .OnDelete(DeleteBehavior.SetNull);

            // Restrict: prevents deleting a user who has created elections,
            // so election ownership/history is never silently lost.
            modelBuilder.Entity<Election>()
                .HasOne(e => e.CreatedByUser)
                .WithMany(u => u.ElectionsCreated)
                .HasForeignKey(e => e.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Option>()
                .HasOne(o => o.Election)
                .WithMany(e => e.Options)
                .HasForeignKey(o => o.ElectionId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<VoteToken>()
                .HasOne(vt => vt.User)
                .WithMany(u => u.VoteTokens)
                .HasForeignKey(vt => vt.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<VoteToken>()
                .HasOne(vt => vt.Election)
                .WithMany(e => e.VoteTokens)
                .HasForeignKey(vt => vt.ElectionId)
                .OnDelete(DeleteBehavior.Cascade);

            // A user can receive at most one token per election.
            modelBuilder.Entity<VoteToken>()
                .HasIndex(vt => new { vt.UserId, vt.ElectionId })
                .IsUnique();
            modelBuilder.Entity<Vote>()
                .HasOne(v => v.Option)
                .WithMany(o => o.Votes)
                .HasForeignKey(v => v.OptionId)
                .OnDelete(DeleteBehavior.Cascade);

            // Restrict: a VoteToken that has already produced a Vote cannot be deleted
            // out from under it.
            modelBuilder.Entity<Vote>()
                .HasOne(v => v.VoteToken)
                .WithOne(vt => vt.Vote)
                .HasForeignKey<Vote>(v => v.VoteTokenId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Vote>()
                .HasOne(v => v.User)
                .WithMany()
                .HasForeignKey(v => v.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            // A VoteToken can be used at most once.
            modelBuilder.Entity<Vote>()
                .HasIndex(v => v.VoteTokenId)
                .IsUnique();

            // Enforce anonymity: exactly one of (VoteTokenId, UserId) must be set.
            modelBuilder.Entity<Vote>()
                .ToTable(t => t.HasCheckConstraint(
                    "CK_Votes_ExactlyOneVoterIdentity",
                    "((VoteTokenId IS NOT NULL AND UserId IS NULL) " +
                    "OR (VoteTokenId IS NULL AND UserId IS NOT NULL))"
                ));

            // A VoterDeclaration only ever exists for a non-anonymous vote (UserId set, VoteTokenId null).
            // Cascade: deleting the vote removes its declaration too.
            modelBuilder.Entity<Vote>()
                .HasOne(v => v.VoterDeclaration)
                .WithOne(vd => vd.Vote)
                .HasForeignKey<VoterDeclaration>(vd => vd.VoteId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<AuditLog>()
                .HasOne(a => a.User)
                .WithMany(u => u.AuditLogs)
                .HasForeignKey(a => a.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // SetNull: elections can be deleted independently of their audit logs.
            modelBuilder.Entity<AuditLog>()
                .HasOne(a => a.Election)
                .WithMany(e => e.AuditLogs)
                .HasForeignKey(a => a.ElectionId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<VoterChangeRecord>()
                .HasOne(r => r.User)
                .WithMany()
                .HasForeignKey(r => r.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<VoterChangeRecord>()
                .HasOne(r => r.Election)
                .WithMany()
                .HasForeignKey(r => r.ElectionId)
                .OnDelete(DeleteBehavior.Cascade);

            // At most one change-budget record per (user, election) - this is what actually
            // enforces the one-change limit, since it survives the Vote row being deleted.
            modelBuilder.Entity<VoterChangeRecord>()
                .HasIndex(r => new { r.UserId, r.ElectionId })
                .IsUnique();
        }
    }
}
