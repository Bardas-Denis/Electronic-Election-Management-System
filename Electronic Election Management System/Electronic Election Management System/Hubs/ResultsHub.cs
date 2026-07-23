using Electronic_Election_Management_System.Data.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace Electronic_Election_Management_System.Hubs
{
    [Authorize]
    public class ResultsHub : Hub
    {
        private readonly IElectionRepository _elections;

        public ResultsHub(IElectionRepository elections)
        {
            _elections = elections;
        }

        public async Task JoinElectionGroup(string electionId)
        {
            if (!System.Guid.TryParse(electionId, out var parsedElectionId))
                throw new HubException("Invalid electionId.");

            var userIdClaim = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userIdClaim, out var userId) ||
                !await _elections.CanUserAccessAsync(parsedElectionId, userId))
            {
                throw new HubException("Election not found.");
            }

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
