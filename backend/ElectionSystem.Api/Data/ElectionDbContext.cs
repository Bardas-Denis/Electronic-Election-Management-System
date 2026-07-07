using Microsoft.EntityFrameworkCore;
using ElectionSystem.Api.Entities;

namespace ElectionSystem.Api.Data
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
        public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // ---------- Users ----------
            modelBuilder.Entity<User>()
                .HasIndex(u => u.Email)
                .IsUnique();

            modelBuilder.Entity<User>()
                .Property(u => u.Role)
                .HasConversion<string>(); // salveaza enum-ul ca text ("Admin"/"Voter")

            // ---------- Elections ----------
            modelBuilder.Entity<Election>()
                .Property(e => e.Type)
                .HasConversion<string>();

            modelBuilder.Entity<Election>()
                .HasOne(e => e.CreatedByUser)
                .WithMany(u => u.ElectionsCreated)
                .HasForeignKey(e => e.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            // ---------- Options ----------
            modelBuilder.Entity<Option>()
                .HasOne(o => o.Election)
                .WithMany(e => e.Options)
                .HasForeignKey(o => o.ElectionId)
                .OnDelete(DeleteBehavior.Cascade);

            // ---------- VoteTokens ----------
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

            // un user primeste cel mult un token per alegere
            modelBuilder.Entity<VoteToken>()
                .HasIndex(vt => new { vt.UserId, vt.ElectionId })
                .IsUnique();

            // ---------- Votes ----------
            modelBuilder.Entity<Vote>()
                .HasOne(v => v.Option)
                .WithMany(o => o.Votes)
                .HasForeignKey(v => v.OptionId)
                .OnDelete(DeleteBehavior.Cascade);

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

            // un token nu poate fi folosit de doua ori
            modelBuilder.Entity<Vote>()
                .HasIndex(v => v.VoteTokenId)
                .IsUnique();

            // Regula de anonimat impusa direct in baza de date:
            // exact una din (VoteTokenId, UserId) trebuie sa fie completata.
            modelBuilder.Entity<Vote>()
                .ToTable(t => t.HasCheckConstraint(
                    "CK_Votes_ExactlyOneVoterIdentity",
                    "((VoteTokenId IS NOT NULL AND UserId IS NULL) " +
                    "OR (VoteTokenId IS NULL AND UserId IS NOT NULL))"
                ));

            // ---------- AuditLogs ----------
            modelBuilder.Entity<AuditLog>()
                .HasOne(a => a.User)
                .WithMany(u => u.AuditLogs)
                .HasForeignKey(a => a.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<AuditLog>()
                .HasOne(a => a.Election)
                .WithMany(e => e.AuditLogs)
                .HasForeignKey(a => a.ElectionId)
                .OnDelete(DeleteBehavior.SetNull);
        }
    }
}
