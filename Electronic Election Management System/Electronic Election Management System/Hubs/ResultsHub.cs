using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Electronic_Election_Management_System.Hubs
{
    [Authorize]
    public class ResultsHub : Hub
    {
        public async Task JoinElectionGroup(string electionId)
        {
            if (!System.Guid.TryParse(electionId, out var parsedElectionId))
                throw new HubException("Invalid electionId.");

            await Groups.AddToGroupAsync(Context.ConnectionId, parsedElectionId.ToString());
        }

        public async Task LeaveElectionGroup(string electionId)
        {
            if (!System.Guid.TryParse(electionId, out var parsedElectionId))
                throw new HubException("Invalid electionId.");

            await Groups.RemoveFromGroupAsync(Context.ConnectionId, parsedElectionId.ToString());
        }
    }
}