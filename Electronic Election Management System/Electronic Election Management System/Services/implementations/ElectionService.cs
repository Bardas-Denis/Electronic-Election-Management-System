using Electronic_Election_Management_System.Constants;
using Electronic_Election_Management_System.Data.Repositories;
using Electronic_Election_Management_System.DTOs;
using Electronic_Election_Management_System.Models;
using System.ComponentModel.DataAnnotations;

namespace Electronic_Election_Management_System.Services
{
    public class ElectionService : IElectionService
    {
        private readonly IElectionRepository _elections;
        private readonly IAuditLogRepository _auditLogs;
        private readonly IVoteRepository _votes;
        private readonly IUserRepository _users;
        private readonly IElectionInvitationRepository _invitations;

        public ElectionService(
            IElectionRepository elections,
            IAuditLogRepository auditLogs,
            IVoteRepository votes,
            IUserRepository users,
            IElectionInvitationRepository invitations)
        {
            _elections = elections;
            _auditLogs = auditLogs;
            _votes = votes;
            _users = users;
            _invitations = invitations;
        }

        public async Task<List<ElectionDto>> GetAllAsync(Guid userId)
        {
            var elections = await _elections.GetVisibleToUserAsync(userId);
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
            var election = await _elections.GetAccessibleByIdWithOptionsAsync(id, userId);
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

            var questions = NormalizeQuestions(request);
            if (!QuestionsAreValid(questions))
                return ServiceResult<ElectionDto>.Fail(ErrorCode.TooFewOptions);

            if (request.EndsAt <= request.StartsAt)
                return ServiceResult<ElectionDto>.Fail(ErrorCode.InvalidDateRange);

            if (!request.IsClosed &&
                (request.InvitedUserIds.Count > 0 || request.InvitedEmails.Count > 0))
            {
                return ServiceResult<ElectionDto>.Fail(ErrorCode.InvitationsRequireClosedElection);
            }

            var invitationResult = request.IsClosed
                ? await BuildInvitationsAsync(
                    Guid.Empty,
                    request.InvitedUserIds,
                    request.InvitedEmails,
                    userId)
                : ServiceResult<List<ElectionInvitation>>.Ok(new List<ElectionInvitation>());
            if (!invitationResult.Success)
                return ServiceResult<ElectionDto>.Fail(invitationResult.ErrorCode!.Value);

            var election = new Election
            {
                CreatedByUserId = userId,
                Title = request.Title.Trim(),
                Description = request.Description,
                Question = questions[0].Text.Trim(),
                Type = type,
                IsAnonymous = request.IsAnonymous,
                IsClosed = request.IsClosed,
                StartsAt = request.StartsAt,
                EndsAt = request.EndsAt,
                Invitations = invitationResult.Data!
            };
            election.Questions = BuildQuestions(questions, election.Id);

            foreach (var invitation in election.Invitations)
                invitation.ElectionId = election.Id;

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

            var questions = NormalizeQuestions(request);
            if (!QuestionsAreValid(questions))
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
            election.Question = questions[0].Text.Trim();
            election.Type = type;
            election.IsAnonymous = request.IsAnonymous;
            election.IsClosed = request.IsClosed;
            election.StartsAt = request.StartsAt;
            election.EndsAt = request.EndsAt;

            var existingOptions = election.Options.ToList();
            var existingQuestions = election.Questions.ToList();
            _elections.RemoveOptions(existingOptions);
            _elections.RemoveQuestions(existingQuestions);
            election.Options.Clear();
            election.Questions.Clear();
            foreach (var question in BuildQuestions(questions, election.Id))
                election.Questions.Add(question);

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

        public async Task<ServiceResult<List<ElectionInvitationDto>>> GetInvitationsAsync(
            Guid electionId,
            Guid userId)
        {
            var election = await _elections.GetByIdAsync(electionId);
            if (election is null)
                return ServiceResult<List<ElectionInvitationDto>>.NotFound();

            if (election.CreatedByUserId != userId)
                return ServiceResult<List<ElectionInvitationDto>>.Fail(ErrorCode.NotAuthorizedToManageInvitations);

            var invitations = await _invitations.GetByElectionAsync(electionId);
            return ServiceResult<List<ElectionInvitationDto>>.Ok(invitations.Select(MapInvitationToDto).ToList());
        }

        public async Task<List<InvitationCandidateDto>> GetInvitationCandidatesAsync(Guid userId)
        {
            var users = await _users.GetAllAsync();
            return users
                .Where(user => user.Id != userId)
                .Select(user => new InvitationCandidateDto
                {
                    Id = user.Id,
                    Email = user.Email
                })
                .ToList();
        }

        public async Task<ServiceResult<List<ElectionInvitationDto>>> InviteAsync(
            Guid electionId,
            InviteToElectionRequest request,
            Guid userId)
        {
            var election = await _elections.GetByIdAsync(electionId);
            if (election is null)
                return ServiceResult<List<ElectionInvitationDto>>.NotFound();

            if (election.CreatedByUserId != userId)
                return ServiceResult<List<ElectionInvitationDto>>.Fail(ErrorCode.NotAuthorizedToManageInvitations);

            if (!election.IsClosed)
                return ServiceResult<List<ElectionInvitationDto>>.Fail(ErrorCode.InvitationsRequireClosedElection);

            var invitationResult = await BuildInvitationsAsync(
                electionId,
                request.UserIds,
                request.Emails,
                userId);
            if (!invitationResult.Success)
                return ServiceResult<List<ElectionInvitationDto>>.Fail(invitationResult.ErrorCode!.Value);

            if (invitationResult.Data!.Count > 0)
            {
                await _invitations.AddRangeAsync(invitationResult.Data);
                await _auditLogs.AddAsync(new AuditLog
                {
                    UserId = userId,
                    ElectionId = electionId,
                    Action = AuditAction.ElectionInvitationsAdded.ToDbValue()
                });
                await _invitations.SaveChangesAsync();
            }

            var invitations = await _invitations.GetByElectionAsync(electionId);
            return ServiceResult<List<ElectionInvitationDto>>.Ok(invitations.Select(MapInvitationToDto).ToList());
        }

        public async Task<ServiceResult<bool>> RemoveInvitationAsync(
            Guid electionId,
            Guid invitationId,
            Guid userId)
        {
            var election = await _elections.GetByIdAsync(electionId);
            if (election is null)
                return ServiceResult<bool>.NotFound();

            if (election.CreatedByUserId != userId)
                return ServiceResult<bool>.Fail(ErrorCode.NotAuthorizedToManageInvitations);

            var invitation = await _invitations.GetByIdAsync(invitationId);
            if (invitation is null || invitation.ElectionId != electionId)
                return ServiceResult<bool>.NotFound();

            _invitations.Remove(invitation);
            await _auditLogs.AddAsync(new AuditLog
            {
                UserId = userId,
                ElectionId = electionId,
                Action = AuditAction.ElectionInvitationRemoved.ToDbValue()
            });
            await _invitations.SaveChangesAsync();
            return ServiceResult<bool>.Ok(true);
        }

        private async Task<ServiceResult<List<ElectionInvitation>>> BuildInvitationsAsync(
            Guid electionId,
            IEnumerable<Guid> rawUserIds,
            IEnumerable<string> rawEmails,
            Guid creatorId)
        {
            var userIds = rawUserIds.Where(id => id != creatorId).Distinct().ToList();
            var users = await _users.GetByIdsAsync(userIds);
            if (users.Count != userIds.Count)
                return ServiceResult<List<ElectionInvitation>>.Fail(ErrorCode.InvitedUserNotFound);

            var normalizedEmails = rawEmails
                .Where(email => !string.IsNullOrWhiteSpace(email))
                .Select(email => email.Trim().ToLowerInvariant())
                .Distinct()
                .ToList();

            var emailValidator = new EmailAddressAttribute();
            if (normalizedEmails.Any(email => !emailValidator.IsValid(email)))
                return ServiceResult<List<ElectionInvitation>>.Fail(ErrorCode.InvalidInvitationEmail);

            var creator = await _users.GetByIdAsync(creatorId);
            if (creator is not null)
                normalizedEmails.Remove(creator.Email);

            var registeredByEmail = (await _users.GetByEmailsAsync(normalizedEmails))
                .ToDictionary(user => user.Email);

            var candidates = users.Select(user => new ElectionInvitation
                {
                    ElectionId = electionId,
                    UserId = user.Id,
                    Email = user.Email,
                    Method = ElectionInvitationMethod.Manual
                })
                .Concat(normalizedEmails.Select(email => new ElectionInvitation
                {
                    ElectionId = electionId,
                    UserId = registeredByEmail.GetValueOrDefault(email)?.Id,
                    Email = email,
                    Method = ElectionInvitationMethod.Email
                }))
                .GroupBy(invitation => invitation.Email)
                .Select(group => group.First())
                .ToList();

            if (electionId != Guid.Empty && candidates.Count > 0)
            {
                var existingEmails = await _invitations.GetExistingEmailsAsync(
                    electionId,
                    candidates.Select(invitation => invitation.Email));
                candidates.RemoveAll(invitation => existingEmails.Contains(invitation.Email));
            }

            return ServiceResult<List<ElectionInvitation>>.Ok(candidates);
        }

        private static bool TryParseType(string raw, out ElectionType type)
            => Enum.TryParse(raw, ignoreCase: true, out type);

        private static List<CreateElectionQuestionDto> NormalizeQuestions(CreateElectionRequest request)
        {
            var supplied = request.Questions
                .Where(q => !string.IsNullOrWhiteSpace(q.Text))
                .ToList();
            if (supplied.Count > 0)
                return supplied;

            return new List<CreateElectionQuestionDto>
            {
                new()
                {
                    Text = request.Question,
                    Options = request.Options
                }
            };
        }

        private static bool QuestionsAreValid(IEnumerable<CreateElectionQuestionDto> questions)
            => questions.Any() && questions.All(q =>
                !string.IsNullOrWhiteSpace(q.Text) &&
                q.Options.Count(o => !string.IsNullOrWhiteSpace(o.Label)) >= 2 &&
                q.Options.All(o => IsValidImage(o.ImageDataUrl)));

        private static bool IsValidImage(string? image)
            => string.IsNullOrWhiteSpace(image) ||
               (image.Length <= 3_000_000 &&
                (image.StartsWith("data:image/png;base64,", StringComparison.OrdinalIgnoreCase) ||
                 image.StartsWith("data:image/jpeg;base64,", StringComparison.OrdinalIgnoreCase) ||
                 image.StartsWith("data:image/webp;base64,", StringComparison.OrdinalIgnoreCase) ||
                 image.StartsWith("data:image/gif;base64,", StringComparison.OrdinalIgnoreCase)));

        private static List<ElectionQuestion> BuildQuestions(
            IEnumerable<CreateElectionQuestionDto> questions,
            Guid? electionId = null)
            => questions.Select((question, questionIndex) => new ElectionQuestion
            {
                ElectionId = electionId ?? Guid.Empty,
                Text = question.Text.Trim(),
                DisplayOrder = questionIndex,
                Options = question.Options
                    .Where(option => !string.IsNullOrWhiteSpace(option.Label))
                    .Select(option => new Option
                    {
                        ElectionId = electionId ?? Guid.Empty,
                        Label = option.Label.Trim(),
                        Description = option.Description?.Trim(),
                        ImageDataUrl = option.ImageDataUrl
                    })
                    .ToList()
            }).ToList();

        private static ElectionDto MapToDto(Election e)
        {
            var questions = e.Questions
                .OrderBy(q => q.DisplayOrder)
                .Select(q => new ElectionQuestionDto
                {
                    Id = q.Id,
                    Text = q.Text,
                    DisplayOrder = q.DisplayOrder,
                    Options = q.Options.Select(MapOptionToDto).ToList()
                })
                .ToList();

            if (questions.Count == 0)
            {
                questions.Add(new ElectionQuestionDto
                {
                    Id = Guid.Empty,
                    Text = e.Question ?? e.Title,
                    Options = e.Options.Select(MapOptionToDto).ToList()
                });
            }

            return new ElectionDto
            {
            Id = e.Id,
            Title = e.Title,
            Description = e.Description,
            Question = e.Question,
            Type = e.Type.ToString(),
            IsAnonymous = e.IsAnonymous,
            IsClosed = e.IsClosed,
            StartsAt = e.StartsAt,
            EndsAt = e.EndsAt,
            Options = questions[0].Options,
            Questions = questions,
            HasUserVoted = false,
            IsExpired = DateTime.UtcNow > e.EndsAt
            };
        }

        private static OptionDto MapOptionToDto(Option option) => new()
        {
            Id = option.Id,
            Label = option.Label,
            Description = option.Description,
            ImageDataUrl = option.ImageDataUrl
        };

        private static ElectionInvitationDto MapInvitationToDto(ElectionInvitation invitation) => new()
        {
            Id = invitation.Id,
            UserId = invitation.UserId,
            Email = invitation.Email,
            Method = invitation.Method.ToString(),
            CreatedAt = invitation.CreatedAt
        };
    }
}
