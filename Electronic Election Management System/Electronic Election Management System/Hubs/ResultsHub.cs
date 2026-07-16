using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Electronic_Election_Management_System.Hubs
{
    [Authorize]
    public class ResultsHub : Hub
    {
        public async Task JoinElectionGroup(string electionId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, electionId);
        }

        public async Task LeaveElectionGroup(string electionId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, electionId);
        }
    }
}