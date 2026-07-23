namespace Electronic_Election_Management_System.Constants
{
    /// <summary>
    /// Identifies the type of auditable action recorded in the audit log.
    /// </summary>
    public enum AuditAction
    {
        AccountCreated,
        Login,
        ElectionCreated,
        ElectionUpdated,
        ElectionDeleted,
        ChangedUserRole,
        DeletedUser,
        ElectionInvitationsAdded,
        ElectionInvitationRemoved,
    }

    /// <summary>
    /// Extension methods that convert an <see cref="AuditAction"/> to its persisted
    /// database string value.  The switch expression is intentional — the mapping
    /// is explicit and independent of C# member names or JSON serialization, so the
    /// stored strings can never drift silently if either changes.
    /// </summary>
    public static class AuditActionValues
    {
        /// <summary>
        /// Returns the exact string value that is (or will be) persisted in the
        /// <c>AuditLog.Action</c> column for the given <paramref name="action"/>.
        /// </summary>
        public static string ToDbValue(this AuditAction action) => action switch
        {
            AuditAction.AccountCreated  => "account_created",
            AuditAction.Login           => "login",
            AuditAction.ElectionCreated => "created_election",
            AuditAction.ElectionUpdated => "updated_election",
            AuditAction.ElectionDeleted => "deleted_election",
            AuditAction.ChangedUserRole => "changed_user_role",
            AuditAction.DeletedUser     => "deleted_user",
            AuditAction.ElectionInvitationsAdded => "election_invitations_added",
            AuditAction.ElectionInvitationRemoved => "election_invitation_removed",
            _ => throw new ArgumentOutOfRangeException(nameof(action), action, null)
        };
    }
}
