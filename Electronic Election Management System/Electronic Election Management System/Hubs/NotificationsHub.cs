using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace Electronic_Election_Management_System.Hubs
{
    // Push-notification hub, one group per authenticated user.
    // All tabs/devices belonging to the same user share the same group,
    // so every active session receives the push.
    [Authorize]
    public class NotificationsHub : Hub
    {
        public override async Task OnConnectedAsync()
        {
            var userId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId is not null)
            {
                // Per-user group: all connections for the same user receive the push
                await Groups.AddToGroupAsync(Context.ConnectionId, userId);
            }

            await base.OnConnectedAsync();
        }
    }
}
