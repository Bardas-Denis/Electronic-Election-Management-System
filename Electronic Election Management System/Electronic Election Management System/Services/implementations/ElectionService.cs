using Electronic_Election_Management_System.Constants;
using Electronic_Election_Management_System.Data.Repositories;
using Electronic_Election_Management_System.DTOs;
using Electronic_Election_Management_System.Models;

namespace Electronic_Election_Management_System.Services
{
    public class ElectionService : IElectionService
    {
        private readonly IElectionRepository _elections;
        private readonly IAuditLogRepository _auditLogs;
        private readonly IVoteRepository _votes;

        public ElectionService(IElectionRepository elections, IAuditLogRepository auditLogs, IVoteRepository votes)
        {
            _elections = elections;
            _auditLogs = auditLogs;
            _votes = votes;
        }

        public async Task<List<ElectionDto>> GetAllAsync(Guid userId)
        {
            var elections = await _elections.GetAllWithOptionsAsync();
            var dtos = new List<ElectionDto>();
            foreach (var election in elections)
            {
                var dto = MapToDto(election);
                dto.HasUserVoted = await _votes.HasUserVotedInElectionAsync(userId, election.Id, election.IsAnonymous);
                dtos.Add(dto);
            }
            return dtos;
        }

        public async Task<List<ElectionDto>> GetCreatedByAsync(Guid userId)
        {
            var elections = await _elections.GetByCreatedByAsync(userId);
            var dtos = new List<ElectionDto>();
            foreach (var election in elections)
            {
                var dto = MapToDto(election);
                dto.HasVotes = await _votes.HasAnyVotesInElectionAsync(election.Id);
                dtos.Add(dto);
            }
            return dtos;
        }

        public async Task<ElectionDto?> GetByIdAsync(Guid id, Guid userId)
        {
            var election = await _elections.GetByIdWithOptionsAsync(id);
            if (election is null)
                return null;

            var dto = MapToDto(election);
            dto.HasUserVoted = await _votes.HasUserVotedInElectionAsync(userId, election.Id, election.IsAnonymous);
            dto.HasVotes = await _votes.HasAnyVotesInElectionAsync(election.Id);
            return dto;
        }

        public async Task<ServiceResult<ElectionDto>> CreateAsync(CreateElectionRequest request, Guid userId)
        {
            if (!TryParseType(request.Type, out var type))
                return ServiceResult<ElectionDto>.Fail(ErrorCode.InvalidElectionType);

            if (request.Options.Count(o => !string.IsNullOrWhiteSpace(o.Label)) < 2)
                return ServiceResult<ElectionDto>.Fail(ErrorCode.TooFewOptions);

            if (request.EndsAt <= request.StartsAt)
                return ServiceResult<ElectionDto>.Fail(ErrorCode.InvalidDateRange);

            var election = new Election
            {
                CreatedByUserId = userId,
                Title = request.Title.Trim(),
                Description = request.Description,
                Question = request.Question?.Trim(),
                Type = type,
                IsAnonymous = request.IsAnonymous,
                StartsAt = request.StartsAt,
                EndsAt = request.EndsAt,
                Options = request.Options
                    .Where(o => !string.IsNullOrWhiteSpace(o.Label))
                    .Select(o => new Option { Label = o.Label.Trim(), Description = o.Description?.Trim() })
                    .ToList()
            };

            await _elections.AddAsync(election);
            await _auditLogs.AddAsync(new AuditLog
            {
                UserId = userId,
                ElectionId = election.Id,
                Action = AuditAction.ElectionCreated.ToDbValue()
            });
            await _elections.SaveChangesAsync();

            return ServiceResult<ElectionDto>.Ok(MapToDto(election));
        }

        public async Task<ServiceResult<ElectionDto>> UpdateAsync(Guid id, UpdateElectionRequest request, Guid userId)
        {
            if (!TryParseType(request.Type, out var type))
                return ServiceResult<ElectionDto>.Fail(ErrorCode.InvalidElectionType);

            var validOptions = request.Options.Where(o => !string.IsNullOrWhiteSpace(o.Label)).ToList();
            if (validOptions.Count < 2)
                return ServiceResult<ElectionDto>.Fail(ErrorCode.TooFewOptions);

            if (request.EndsAt <= request.StartsAt)
                return ServiceResult<ElectionDto>.Fail(ErrorCode.InvalidDateRange);

            var election = await _elections.GetByIdWithOptionsAsync(id);
            if (election is null)
                return ServiceResult<ElectionDto>.NotFound(ErrorCode.ResourceNotFound);

            if (election.CreatedByUserId != userId)
                return ServiceResult<ElectionDto>.Fail(ErrorCode.NotAuthorizedToEdit);

            if (await _votes.HasAnyVotesInElectionAsync(election.Id))
                return ServiceResult<ElectionDto>.Fail(ErrorCode.ElectionHasVotes);

            election.Title = request.Title.Trim();
            election.Description = request.Description;
            election.Question = request.Question?.Trim();
            election.Type = type;
            election.IsAnonymous = request.IsAnonymous;
            election.StartsAt = request.StartsAt;
            election.EndsAt = request.EndsAt;

            // Update existing options safely (keeps their IDs and prevents tracking conflicts)
            var optionsList = election.Options.ToList();
            for (int i = 0; i < validOptions.Count; i++)
            {
                if (i < optionsList.Count)
                {
                    optionsList[i].Label = validOptions[i].Label.Trim();
                    optionsList[i].Description = validOptions[i].Description?.Trim();
                }
                else
                {
                    election.Options.Add(new Option 
                    { 
                        Label = validOptions[i].Label.Trim(), 
                        Description = validOptions[i].Description?.Trim(),
                        ElectionId = election.Id 
                    });
                }
            }

            if (optionsList.Count > validOptions.Count)
            {
                var toRemove = optionsList.Skip(validOptions.Count).ToList();
                foreach (var opt in toRemove)
                {
                    election.Options.Remove(opt);
                }
                _elections.RemoveOptions(toRemove);
            }

            await _auditLogs.AddAsync(new AuditLog
            {
                UserId = userId,
                ElectionId = election.Id,
                Action = AuditAction.ElectionUpdated.ToDbValue()
            });
            await _elections.SaveChangesAsync();

            return ServiceResult<ElectionDto>.Ok(MapToDto(election));
        }

        public async Task<ServiceResult<bool>> DeleteAsync(Guid id, Guid userId)
        {
            var election = await _elections.GetByIdAsync(id);
            if (election is null)
                return ServiceResult<bool>.NotFound(ErrorCode.ResourceNotFound);

            if (election.CreatedByUserId != userId)
                return ServiceResult<bool>.Fail(ErrorCode.NotAuthorizedToDelete);

            // Audit log written before delete so we still have the title.
            await _auditLogs.AddAsync(new AuditLog
            {
                UserId = userId,
                ElectionId = null,
                Action = $"{AuditAction.ElectionDeleted.ToDbValue()}:{election.Title}"
            });

            _elections.Remove(election);
            await _elections.SaveChangesAsync();

            return ServiceResult<bool>.Ok(true);
        }

        

        private static bool TryParseType(string raw, out ElectionType type)
            => Enum.TryParse(raw, ignoreCase: true, out type);

        private static ElectionDto MapToDto(Election e) => new()
        {
            Id = e.Id,
            Title = e.Title,
            Description = e.Description,
            Question = e.Question,
            Type = e.Type.ToString(),
            IsAnonymous = e.IsAnonymous,
            StartsAt = e.StartsAt,
            EndsAt = e.EndsAt,
            Options = e.Options.Select(o => new OptionDto { Id = o.Id, Label = o.Label, Description = o.Description }).ToList(),
            HasUserVoted = false,
            IsExpired = DateTime.UtcNow > e.EndsAt
        };
    }
}
