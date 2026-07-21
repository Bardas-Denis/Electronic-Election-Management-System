namespace Electronic_Election_Management_System.Services
{
    public interface IUserNotifier
    {
        /// <summary>
        /// Sends a push notification to all active sessions of the specified user,
        /// informing them that their role has been changed.
        /// </summary>
        Task NotifyRoleChangedAsync(Guid userId);
    }
}
