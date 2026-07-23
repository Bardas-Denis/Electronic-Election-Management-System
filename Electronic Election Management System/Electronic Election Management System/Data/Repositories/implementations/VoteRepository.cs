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

        public async Task<bool> HasUserVotedInElectionAsync(Guid userId, Guid electionId, bool isAnonymous)
        {
            if (isAnonymous)
            {
                var token = await _db.VoteTokens
                    .FirstOrDefaultAsync(t => t.UserId == userId && t.ElectionId == electionId);
                return token?.IsUsed == true;
            }

            return await HasUserVotedAsync(userId, electionId);
        }

        public async Task AddVoteAsync(Vote vote)
            => await _db.Votes.AddAsync(vote);

        public async Task AddVoterDeclarationAsync(VoterDeclaration declaration)
            => await _db.VoterDeclarations.AddAsync(declaration);

        public Task<bool> HasCnpBeenUsedInElectionAsync(string cnp, Guid electionId)
            => _db.Votes
                .Where(v => v.Option!.ElectionId == electionId && v.VoterDeclaration != null && v.VoterDeclaration.Cnp == cnp)
                .AnyAsync();

        public Task<bool> HasAnyVotesInElectionAsync(Guid electionId)
            => _db.Votes.AnyAsync(v => v.Option!.ElectionId == electionId);

        public Task<List<Vote>> GetUserVotesInElectionAsync(Guid userId, Guid electionId)
            => _db.Votes
                .Include(v => v.Option)
                .Include(v => v.VoterDeclaration)
                .Where(v => v.UserId == userId && v.Option!.ElectionId == electionId)
                .ToListAsync();

        public Task<VoteToken?> GetVoteTokenWithVotesAsync(Guid userId, Guid electionId)
            => _db.VoteTokens
                .Include(t => t.Votes)
                    .ThenInclude(v => v.Option)
                .FirstOrDefaultAsync(t => t.UserId == userId && t.ElectionId == electionId);

        public void RemoveVote(Vote vote)
            => _db.Votes.Remove(vote);

        public void RemoveVotes(IEnumerable<Vote> votes)
            => _db.Votes.RemoveRange(votes);

        public async Task<int> GetChangeCountAsync(Guid userId, Guid electionId)
        {
            var record = await _db.VoterChangeRecords
                .FirstOrDefaultAsync(r => r.UserId == userId && r.ElectionId == electionId);
            return record?.ChangeCount ?? 0;
        }

        public async Task IncrementChangeCountAsync(Guid userId, Guid electionId)
        {
            var record = await _db.VoterChangeRecords
                .FirstOrDefaultAsync(r => r.UserId == userId && r.ElectionId == electionId);

            if (record is null)
            {
                await _db.VoterChangeRecords.AddAsync(new VoterChangeRecord
                {
                    UserId = userId,
                    ElectionId = electionId,
                    ChangeCount = 1
                });
            }
            else
            {
                record.ChangeCount += 1;
            }
        }

        public Task SaveChangesAsync()
            => _db.SaveChangesAsync();
    }
}
