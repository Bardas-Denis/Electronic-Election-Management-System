namespace Electronic_Election_Management_System.Constants;

/// <summary>
/// Structured log message templates used across services.
/// Named holes must stay stable — renaming a hole is a breaking change for log queries.
/// </summary>
public static class LogMessages
{
    //Auth
    public const string UserRegistered       = "New user registered: {Email} (UserId: {UserId})";
    public const string UserLoggedIn         = "User logged in: {Email} (UserId: {UserId})";
    public const string FailedLoginAttempt   = "Failed login attempt for email: {Email}";

    //Elections
    public const string ElectionCreated              = "Election created: {Title} (ElectionId: {ElectionId}, CreatedBy: {UserId})";
    public const string ElectionUpdated              = "Election updated: {ElectionId} by UserId {UserId}";
    public const string ElectionDeleted              = "Election deleted: '{Title}' (ElectionId: {ElectionId}) by UserId {UserId}";
    public const string ElectionUpdateUnauthorized   = "Unauthorized update attempt on ElectionId {ElectionId} by UserId {UserId}";
    public const string ElectionDeleteUnauthorized   = "Unauthorized delete attempt on ElectionId {ElectionId} by UserId {UserId}";
    public const string InvitationsAdded             = "{Count} invitation(s) added to ElectionId {ElectionId} by UserId {UserId}";
    public const string InvitationRemoved            = "Invitation {InvitationId} removed from ElectionId {ElectionId} by UserId {UserId}";

    //Votes
    public const string VoteCast            = "User {UserId} voted in Election {ElectionId}";
    public const string VoteUpdated         = "User {UserId} updated vote in Election {ElectionId}";
    public const string VoteDeleted         = "User {UserId} deleted vote in Election {ElectionId}";
    public const string SignalRBroadcastFailed = "SignalR broadcast failed for ElectionId {ElectionId}";

    //Users
    public const string UserRoleChanged          = "Role changed: UserId {TargetId} → {NewRole} by AdminId {AdminId}";
    public const string LastAdminDemoteBlocked   = "Attempt to demote last admin UserId {TargetId} blocked by UserId {AdminId}";
    public const string UserDeleted              = "User deleted: UserId {TargetId} by AdminId {AdminId}";
    public const string UserDeleteConstraintFail = "Unexpected constraint violation deleting UserId {TargetId}";

    //Labels
    public const string LabelCreated     = "Label created: '{Name}' (LabelId: {LabelId})";
    public const string LabelDeleted     = "Label deleted: '{Name}' (LabelId: {LabelId})";
    public const string LabelsAssigned   = "{Count} label(s) assigned to UserId {UserId} by AdminId {AdminId}";
    public const string LabelRemoved     = "Label {LabelId} removed from UserId {UserId}";

    //Security
    public const string RevokedTokenRejected = "Revoked token rejected for UserId {UserId}";

    //Infrastructure
    public const string UnhandledException = "Unhandled exception on {Method} {Path}";
}

public static class NotificationMessages
{
    public const string ElectionInvitationSubject = "Election Invitation";
    public const string ElectionUpdatedSubject = "Election Updated";

    public const string InvitationType = "Invitation";
    public const string ElectionUpdatedType = "ElectionUpdated";

    public static string InvitationNotification(string title) => 
        $"You have been invited to participate in the election '{title}'.";
        
    public static string ElectionUpdatedNotification(string title) => 
        $"The election '{title}' has been updated (e.g., status or deadlines changed).";

    public static string InvitationEmailRegistered(string title) => 
        $"You have been invited to participate in the election '{title}'.";

    public static string InvitationEmailUnregistered(string title) => 
        $"You have been invited to participate in the election '{title}' but you don't have an account yet. You can create one here: http://localhost:4200/auth/register";

    public static string ElectionUpdatedEmailRegistered(string title) => 
        $"The election '{title}' has been updated. Please check the platform for the latest details.";

    public static string ElectionUpdatedEmailUnregistered(string title) => 
        $"The election '{title}' has been updated. You were invited to it, but you don't have an account yet. You can create one here: http://localhost:4200/auth/register";
}
