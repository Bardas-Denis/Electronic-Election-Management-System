using Electronic_Election_Management_System.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace Electronic_Election_Management_System.Services
{
    // Sends the "RoleChanged" event to the per-user group in NotificationsHub.
    // If the user has no active connection, SendAsync is a no-op — no errors thrown.
    public class SignalRUserNotifier : IUserNotifier
    {
        private readonly IHubContext<NotificationsHub> _hubContext;

        public SignalRUserNotifier(IHubContext<NotificationsHub> hubContext)
        {
            _hubContext = hubContext;
        }

        public async Task NotifyRoleChangedAsync(Guid userId)
        {
            await _hubContext.Clients.Group(userId.ToString()).SendAsync("RoleChanged");
        }
    }
}
