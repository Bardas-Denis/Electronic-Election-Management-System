using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Electronic_Election_Management_System.Models;

namespace Electronic_Election_Management_System.Data
{
    public class ElectionDbContext : DbContext
    {
        public ElectionDbContext(DbContextOptions options)
            : base(options) { }

        public DbSet<User> Users => Set<User>();
        public DbSet<Election> Elections => Set<Election>();
        public DbSet<Option> Options => Set<Option>();
        public DbSet<VoteToken> VoteTokens => Set<VoteToken>();
        public DbSet<Vote> Votes => Set<Vote>();
        public DbSet<VoterDeclaration> VoterDeclarations => Set<VoterDeclaration>();
        public DbSet<UserDetails> UserDetails => Set<UserDetails>();
        public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
        public DbSet<VoterChangeRecord> VoterChangeRecords => Set<VoterChangeRecord>();
        public DbSet<ElectionInvitation> ElectionInvitations => Set<ElectionInvitation>();
        public DbSet<ElectionQuestion> ElectionQuestions => Set<ElectionQuestion>();
        public DbSet<Label> Labels => Set<Label>();
        public DbSet<UserLabel> UserLabels => Set<UserLabel>();
        public DbSet<Notification> Notifications => Set<Notification>();
        public DbSet<ScoringScheme> ScoringSchemes => Set<ScoringScheme>();
        public DbSet<ElectionImage> ElectionImages => Set<ElectionImage>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<User>()
                .HasIndex(u => u.Email)
                .IsUnique();

            // Explicit lambdas rather than HasConversion<string>(): that overload caches a
            // switch-case expression which breaks when a new enum value is added without a
            // clean rebuild.
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

            // Restrict: election ownership must never be silently lost with the user.
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

            modelBuilder.Entity<ElectionQuestion>()
                .HasOne(q => q.Election)
                .WithMany(e => e.Questions)
                .HasForeignKey(q => q.ElectionId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Option>()
                .HasOne(o => o.Question)
                .WithMany(q => q.Options)
                .HasForeignKey(o => o.QuestionId)
                .OnDelete(DeleteBehavior.Cascade);

            // Explicit lambdas for the same reason as User.Role above.
            modelBuilder.Entity<ElectionQuestion>()
                .Property(q => q.QuestionType)
                .HasConversion(
                    type => type.ToString(),
                    value => (QuestionType)Enum.Parse(typeof(QuestionType), value));

            modelBuilder.Entity<Vote>()
                .HasOne(v => v.Question)
                .WithMany(q => q.Votes)
                .HasForeignKey(v => v.QuestionId)
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

            // Restrict: a token that already produced a vote cannot be deleted under it.
            modelBuilder.Entity<Vote>()
                .HasOne(v => v.VoteToken)
                .WithMany(vt => vt.Votes)
                .HasForeignKey(v => v.VoteTokenId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Vote>()
                .HasOne(v => v.User)
                .WithMany()
                .HasForeignKey(v => v.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            // Not unique: a token produces one vote row per question.
            modelBuilder.Entity<Vote>().HasIndex(v => v.VoteTokenId);

            // Enforce anonymity: exactly one of (VoteTokenId, UserId) must be set.
            modelBuilder.Entity<Vote>()
                .ToTable(t => t.HasCheckConstraint(
                    "CK_Votes_ExactlyOneVoterIdentity",
                    "((\"VoteTokenId\" IS NOT NULL AND \"UserId\" IS NULL) " +
                    "OR (\"VoteTokenId\" IS NULL AND \"UserId\" IS NOT NULL))"
                ));

            // A vote is either a Choice-question option pick, or a FreeText-question
            // answer (QuestionId + AnswerText) - never both, never neither.
            modelBuilder.Entity<Vote>()
                .ToTable(t => t.HasCheckConstraint(
                    "CK_Votes_ExactlyOneAnswerKind",
                    "((\"OptionId\" IS NOT NULL AND \"QuestionId\" IS NULL AND \"AnswerText\" IS NULL) " +
                    "OR (\"OptionId\" IS NULL AND \"QuestionId\" IS NOT NULL AND \"AnswerText\" IS NOT NULL))"
                ));

            // Exists only for a non-anonymous vote, and goes when that vote does.
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

            // What actually enforces the one-change limit: unlike the Vote row, this survives
            // the voter deleting their vote.
            modelBuilder.Entity<VoterChangeRecord>()
                .HasIndex(r => new { r.UserId, r.ElectionId })
                .IsUnique();

            // ── Ballot images ────────────────────────────────────────────────────
            // Must stay above the UTC converter loop, or CreatedAt misses the converter.

            modelBuilder.Entity<ElectionImage>()
                .Property(i => i.Sha256)
                .HasMaxLength(64)
                .IsRequired();

            modelBuilder.Entity<ElectionImage>()
                .Property(i => i.ContentType)
                .HasMaxLength(50)
                .IsRequired();

            modelBuilder.Entity<ElectionImage>()
                .Property(i => i.Content)
                .IsRequired();

            // Nullable FK because an image is uploaded before the election exists.
            modelBuilder.Entity<ElectionImage>()
                .HasOne(i => i.Election)
                .WithMany()
                .HasForeignKey(i => i.ElectionId)
                .OnDelete(DeleteBehavior.Cascade);

            // Only reaches unclaimed drafts in practice: UserService refuses to delete a user who
            // has created elections, so claimed images cannot be orphaned this way.
            modelBuilder.Entity<ElectionImage>()
                .HasOne(i => i.UploadedByUser)
                .WithMany()
                .HasForeignKey(i => i.UploadedByUserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Serves the draft sweep and the release of an edited election's dropped pictures.
            modelBuilder.Entity<ElectionImage>()
                .HasIndex(i => i.ElectionId);

            // SetNull, not Cascade: losing an image must never remove the option or question it
            // illustrates from the ballot.
            modelBuilder.Entity<Option>()
                .HasOne(o => o.Image)
                .WithMany()
                .HasForeignKey(o => o.ImageId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<ElectionQuestion>()
                .HasOne(q => q.Image)
                .WithMany()
                .HasForeignKey(q => q.ImageId)
                .OnDelete(DeleteBehavior.SetNull);

            // ── Force every DateTime to round-trip as UTC ────────────────────────
            // Postgres stores "timestamp without time zone" and hands Kind back as Unspecified.
            // System.Text.Json omits the "Z" suffix for anything but Kind=Utc, and the browser
            // then parses the result as local time - which is how timestamps ended up off by
            // the local offset. Tagging Kind on the way out restores the suffix.
            var utcConverter = new ValueConverter<DateTime, DateTime>(
                toDb => toDb.Kind == DateTimeKind.Utc ? toDb : toDb.ToUniversalTime(),
                fromDb => DateTime.SpecifyKind(fromDb, DateTimeKind.Utc));

            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                foreach (var property in entityType.GetProperties())
                {
                    if (property.ClrType == typeof(DateTime))
                    {
                        property.SetValueConverter(utcConverter);
                    }
                    else if (property.ClrType == typeof(DateTime?))
                    {
                        property.SetValueConverter(new ValueConverter<DateTime?, DateTime?>(
                            toDb => toDb.HasValue
                                ? (toDb.Value.Kind == DateTimeKind.Utc ? toDb.Value : toDb.Value.ToUniversalTime())
                                : toDb,
                            fromDb => fromDb.HasValue
                                ? DateTime.SpecifyKind(fromDb.Value, DateTimeKind.Utc)
                                : fromDb));
                    }
                }
            }

            // UserDetails: one editable profile row per user, null until first PUT.
            // Cascade: deleting a User removes their UserDetails row.
            modelBuilder.Entity<User>()
                .HasOne(u => u.UserDetails)
                .WithOne(ud => ud.User)
                .HasForeignKey<UserDetails>(ud => ud.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<UserDetails>()
                .HasIndex(ud => ud.UserId)
                .IsUnique();

            // Label: unique name
            modelBuilder.Entity<Label>()
                .HasIndex(l => l.Name)
                .IsUnique();

            // UserLabel: composite PK (UserId, LabelId) — also serves as the unique constraint
            modelBuilder.Entity<UserLabel>()
                .HasKey(ul => new { ul.UserId, ul.LabelId });

            // UserLabel → User (the labelled user): cascade so assignments are removed when a user is deleted
            modelBuilder.Entity<UserLabel>()
                .HasOne(ul => ul.User)
                .WithMany(u => u.UserLabels)
                .HasForeignKey(ul => ul.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // UserLabel → Label: cascade so assignments are removed when a label is deleted
            modelBuilder.Entity<UserLabel>()
                .HasOne(ul => ul.Label)
                .WithMany(l => l.UserLabels)
                .HasForeignKey(ul => ul.LabelId)
                .OnDelete(DeleteBehavior.Cascade);

            // UserLabel → User (admin who assigned): restrict so admin records are not lost
            modelBuilder.Entity<UserLabel>()
                .HasOne(ul => ul.Admin)
                .WithMany()
                .HasForeignKey(ul => ul.AssignedBy)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Notification>()
                .HasOne(n => n.User)
                .WithMany(u => u.Notifications)
                .HasForeignKey(n => n.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ScoringScheme>()
                .HasOne(ss => ss.CreatedByUser)
                .WithMany()
                .HasForeignKey(ss => ss.CreatedByUserId)
                .OnDelete(DeleteBehavior.SetNull);

            var pointsComparer = new Microsoft.EntityFrameworkCore.ChangeTracking.ValueComparer<List<int>>(
                (c1, c2) => c1 != null && c2 != null ? c1.SequenceEqual(c2) : c1 == c2,
                c => c == null ? 0 : c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                c => c == null ? new List<int>() : c.ToList());

            modelBuilder.Entity<ScoringScheme>()
                .Property(ss => ss.Points)
                .HasConversion(
                    v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                    v => System.Text.Json.JsonSerializer.Deserialize<List<int>>(v, (System.Text.Json.JsonSerializerOptions?)null) ?? new List<int>(),
                    pointsComparer);

            modelBuilder.Entity<ElectionQuestion>()
                .HasOne(q => q.ScoringScheme)
                .WithMany()
                .HasForeignKey(q => q.ScoringSchemeId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}