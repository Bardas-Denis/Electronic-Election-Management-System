namespace Electronic_Election_Management_System.Constants;

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
