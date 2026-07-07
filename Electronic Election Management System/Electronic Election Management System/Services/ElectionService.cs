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

        public async Task<ElectionDto?> GetByIdAsync(Guid id)
        {
            var election = await _elections.GetByIdWithOptionsAsync(id);
            return election is null ? null : MapToDto(election);
        }

        public async Task<ServiceResult<ElectionDto>> CreateAsync(CreateElectionRequest request, Guid userId)
        {
            if (!TryParseType(request.Type, out var type))
                return ServiceResult<ElectionDto>.Fail("Tip invalid. Valorile acceptate sunt 'Politic' sau 'Comercial'.");

            if (request.OptionLabels.Count(l => !string.IsNullOrWhiteSpace(l)) < 2)
                return ServiceResult<ElectionDto>.Fail("O alegere trebuie sa aiba cel putin 2 optiuni de vot.");

            if (request.EndsAt <= request.StartsAt)
                return ServiceResult<ElectionDto>.Fail("Data de sfarsit trebuie sa fie dupa data de inceput.");

            var election = new Election
            {
                CreatedByUserId = userId,
                Title = request.Title.Trim(),
                Description = request.Description,
                Type = type,
                IsAnonymous = request.IsAnonymous,
                StartsAt = request.StartsAt,
                EndsAt = request.EndsAt,
                Options = request.OptionLabels
                    .Where(l => !string.IsNullOrWhiteSpace(l))
                    .Select(l => new Option { Label = l.Trim() })
                    .ToList()
            };

            await _elections.AddAsync(election);
            await _auditLogs.AddAsync(new AuditLog
            {
                UserId = userId,
                ElectionId = election.Id,
                Action = "a_creat_alegere"
            });
            await _elections.SaveChangesAsync();

            return ServiceResult<ElectionDto>.Ok(MapToDto(election));
        }

        public async Task<ServiceResult<ElectionDto>> UpdateAsync(Guid id, UpdateElectionRequest request, Guid userId)
        {
            if (!TryParseType(request.Type, out var type))
                return ServiceResult<ElectionDto>.Fail("Tip invalid. Valorile acceptate sunt 'Politic' sau 'Comercial'.");

            var validLabels = request.OptionLabels.Where(l => !string.IsNullOrWhiteSpace(l)).ToList();
            if (validLabels.Count < 2)
                return ServiceResult<ElectionDto>.Fail("O alegere trebuie sa aiba cel putin 2 optiuni de vot.");

            if (request.EndsAt <= request.StartsAt)
                return ServiceResult<ElectionDto>.Fail("Data de sfarsit trebuie sa fie dupa data de inceput.");

            var election = await _elections.GetByIdWithOptionsAsync(id);
            if (election is null)
                return ServiceResult<ElectionDto>.NotFound("Alegerea nu a fost gasita.");

            election.Title = request.Title.Trim();
            election.Description = request.Description;
            election.Type = type;
            election.IsAnonymous = request.IsAnonymous;
            election.StartsAt = request.StartsAt;
            election.EndsAt = request.EndsAt;

            // Update existing options safely (keeps their IDs and prevents tracking conflicts)
            var optionsList = election.Options.ToList();
            for (int i = 0; i < validLabels.Count; i++)
            {
                if (i < optionsList.Count)
                {
                    optionsList[i].Label = validLabels[i].Trim();
                }
                else
                {
                    election.Options.Add(new Option { Label = validLabels[i].Trim(), ElectionId = election.Id });
                }
            }

            if (optionsList.Count > validLabels.Count)
            {
                var toRemove = optionsList.Skip(validLabels.Count).ToList();
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
                Action = "a_modificat_alegere"
            });
            await _elections.SaveChangesAsync();

            return ServiceResult<ElectionDto>.Ok(MapToDto(election));
        }

        public async Task<ServiceResult<bool>> DeleteAsync(Guid id, Guid userId)
        {
            var election = await _elections.GetByIdAsync(id);
            if (election is null)
                return ServiceResult<bool>.NotFound("Alegerea nu a fost gasita.");

            // Audit log written before delete so we still have the title.
            await _auditLogs.AddAsync(new AuditLog
            {
                UserId = userId,
                ElectionId = null, // election is about to be deleted; don't keep a dangling FK
                Action = $"a_sters_alegere:{election.Title}"
            });

            _elections.Remove(election);
            await _elections.SaveChangesAsync();

            return ServiceResult<bool>.Ok(true);
        }

        // ---- Helpers -------------------------------------------------------

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
            Options = e.Options.Select(o => new OptionDto { Id = o.Id, Label = o.Label }).ToList(),
            HasUserVoted = false // populated in Stage 3 (voting flow)
        };
    }
}
