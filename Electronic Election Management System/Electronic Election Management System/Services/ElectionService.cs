using Electronic_Election_Management_System.Data.Repositories;
using Electronic_Election_Management_System.DTOs;
using Electronic_Election_Management_System.Models;

namespace Electronic_Election_Management_System.Services
{
    public class ElectionService : IElectionService
    {
        private readonly IElectionRepository _elections;
        private readonly IAuditLogRepository _auditLogs;

        public ElectionService(IElectionRepository elections, IAuditLogRepository auditLogs)
        {
            _elections = elections;
            _auditLogs = auditLogs;
        }

        public async Task<List<ElectionDto>> GetAllAsync()
        {
            var elections = await _elections.GetAllWithOptionsAsync();
            return elections.Select(MapToDto).ToList();
        }

        public async Task<List<ElectionDto>> GetCreatedByAsync(Guid userId)
        {
            var elections = await _elections.GetByCreatedByAsync(userId);
            return elections.Select(MapToDto).ToList();
        }

        public async Task<ElectionDto?> GetByIdAsync(Guid id)
        {
            var election = await _elections.GetByIdWithOptionsAsync(id);
            return election is null ? null : MapToDto(election);
        }

        public async Task<ServiceResult<ElectionDto>> CreateAsync(CreateElectionRequest request, Guid userId)
        {
            if (!TryParseType(request.Type, out var type))
                return ServiceResult<ElectionDto>.Fail("Invalid type. Accepted values are 'Politic' or 'Comercial'.");

            if (request.Options.Count(o => !string.IsNullOrWhiteSpace(o.Label)) < 2)
                return ServiceResult<ElectionDto>.Fail("An election needs to have at least 2 vote options");

            if (request.EndsAt <= request.StartsAt)
                return ServiceResult<ElectionDto>.Fail("End Date needs to be after start Date");

            var election = new Election
            {
                CreatedByUserId = userId,
                Title = request.Title.Trim(),
                Description = request.Description,
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
                Action = "created_election"
            });
            await _elections.SaveChangesAsync();

            return ServiceResult<ElectionDto>.Ok(MapToDto(election));
        }

        public async Task<ServiceResult<ElectionDto>> UpdateAsync(Guid id, UpdateElectionRequest request, Guid userId)
        {
            if (!TryParseType(request.Type, out var type))
                return ServiceResult<ElectionDto>.Fail("Invalid type. Accepted values are 'Politic' or 'Comercial'.");

            var validOptions = request.Options.Where(o => !string.IsNullOrWhiteSpace(o.Label)).ToList();
            if (validOptions.Count < 2)
                return ServiceResult<ElectionDto>.Fail("An election needs to have at least 2 vote options");

            if (request.EndsAt <= request.StartsAt)
                return ServiceResult<ElectionDto>.Fail("End Date needs to be after start Date");

            var election = await _elections.GetByIdWithOptionsAsync(id);
            if (election is null)
                return ServiceResult<ElectionDto>.NotFound("Election not found.");

            if (election.CreatedByUserId != userId)
                return ServiceResult<ElectionDto>.Fail("Nu ești autorizat să editezi această alegere.");

            election.Title = request.Title.Trim();
            election.Description = request.Description;
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
                Action = "updated_election"
            });
            await _elections.SaveChangesAsync();

            return ServiceResult<ElectionDto>.Ok(MapToDto(election));
        }

        public async Task<ServiceResult<bool>> DeleteAsync(Guid id, Guid userId)
        {
            var election = await _elections.GetByIdAsync(id);
            if (election is null)
                return ServiceResult<bool>.NotFound("Election not found.");

            if (election.CreatedByUserId != userId)
                return ServiceResult<bool>.Fail("Nu ești autorizat să ștergi această alegere.");

            // Audit log written before delete so we still have the title.
            await _auditLogs.AddAsync(new AuditLog
            {
                UserId = userId,
                ElectionId = null,
                Action = $"deleted_election:{election.Title}"
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
            Type = e.Type.ToString(),
            IsAnonymous = e.IsAnonymous,
            StartsAt = e.StartsAt,
            EndsAt = e.EndsAt,
            Options = e.Options.Select(o => new OptionDto { Id = o.Id, Label = o.Label, Description = o.Description }).ToList(),
            HasUserVoted = false
        };
    }
}
