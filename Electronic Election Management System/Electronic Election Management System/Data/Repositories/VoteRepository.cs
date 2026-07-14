using Electronic_Election_Management_System.Models;
using Microsoft.EntityFrameworkCore;

namespace Electronic_Election_Management_System.Data.Repositories
{
    public class VoteRepository : IVoteRepository
    {
        private readonly ElectionDbContext _db;

        public VoteRepository(ElectionDbContext db)
        {
            _db = db;
        }

        public Task<VoteToken?> GetVoteTokenAsync(Guid userId, Guid electionId)
            => _db.VoteTokens.FirstOrDefaultAsync(t => t.UserId == userId && t.ElectionId == electionId);

        public async Task AddVoteTokenAsync(VoteToken token)
            => await _db.VoteTokens.AddAsync(token);

        public Task<bool> HasUserVotedAsync(Guid userId, Guid electionId)
            => _db.Votes
                .Where(v => v.UserId == userId)
                .AnyAsync(v => v.Option!.ElectionId == electionId);

        public async Task AddVoteAsync(Vote vote)
            => await _db.Votes.AddAsync(vote);

        public async Task AddVoterDeclarationAsync(VoterDeclaration declaration)
            => await _db.VoterDeclarations.AddAsync(declaration);

        public Task<bool> HasCnpBeenUsedInElectionAsync(string cnp, Guid electionId)
            => _db.Votes
                .Where(v => v.Option!.ElectionId == electionId && v.VoterDeclaration != null && v.VoterDeclaration.Cnp == cnp)
                .AnyAsync();

        public Task SaveChangesAsync()
            => _db.SaveChangesAsync();
    }
}
