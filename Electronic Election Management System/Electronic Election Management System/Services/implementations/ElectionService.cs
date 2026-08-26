using System.Text.Json;
using Electronic_Election_Management_System.Constants;
using Electronic_Election_Management_System.Data.Repositories;
using Electronic_Election_Management_System.DTOs;
using Electronic_Election_Management_System.Models;
using System.ComponentModel.DataAnnotations;
using Electronic_Election_Management_System.Services.interfaces;

namespace Electronic_Election_Management_System.Services
{
    public class ElectionService : IElectionService
    {
        private readonly IElectionRepository _elections;
        private readonly IAuditLogRepository _auditLogs;
        private readonly IVoteRepository _votes;
        private readonly IUserRepository _users;
        private readonly IElectionInvitationRepository _invitations;
        private readonly ILabelRepository _labels;
        private readonly ILogger<ElectionService> _logger;
        private readonly INotificationRepository _notifications;
        private readonly IEmailService _emailService;
        private readonly IImageService _images;

        public ElectionService(
            IElectionRepository elections,
            IAuditLogRepository auditLogs,
            IVoteRepository votes,
            IUserRepository users,
            IElectionInvitationRepository invitations,
            ILabelRepository labels,
            ILogger<ElectionService> logger,
            INotificationRepository notifications,
            IEmailService emailService,
            IImageService images)
        {
            _elections = elections;
            _auditLogs = auditLogs;
            _votes = votes;
            _users = users;
            _invitations = invitations;
            _labels = labels;
            _logger = logger;
            _notifications = notifications;
            _emailService = emailService;
            _images = images;
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

            if (!RankCountsAreValid(questions))
                return ServiceResult<ElectionDto>.Fail(ErrorCode.InvalidRankCount);

            if (request.EndsAt <= request.StartsAt)
                return ServiceResult<ElectionDto>.Fail(ErrorCode.InvalidDateRange);

            // Checked before anything is written, so a bad reference fails the create outright
            // rather than leaving a half-built election behind.
            var imageIds = CollectImageIds(questions);
            var imagesUsable = await _images.ValidateClaimableAsync(imageIds, userId, electionId: null);
            if (!imagesUsable.Success)
                return ServiceResult<ElectionDto>.Fail(imagesUsable.ErrorCode!.Value);

            if (!request.IsClosed &&
                (request.InvitedUserIds.Count > 0 ||
                 request.InvitedEmails.Count > 0 ||
                 request.InvitedAudienceGroups.Count > 0))
            {
                return ServiceResult<ElectionDto>.Fail(ErrorCode.InvitationsRequireClosedElection);
            }

            ServiceResult<List<ElectionInvitation>> invitationResult;
            if (request.IsClosed)
            {
                List<Guid> targetUserIds;
                if (request.InvitedUserIds.Count > 0)
                {
                    targetUserIds = request.InvitedUserIds.Distinct().ToList();
                }
                else
                {
                    var audienceResult = await ExpandAudienceGroupsAsync(
                        request.InvitedUserIds,
                        request.InvitedAudienceGroups);
                    if (!audienceResult.Success)
                        return ServiceResult<ElectionDto>.Fail(audienceResult.ErrorCode!.Value);
                    targetUserIds = audienceResult.Data!;
                }

                invitationResult = await BuildInvitationsAsync(
                    Guid.Empty,
                    targetUserIds,
                    request.InvitedEmails,
                    userId);
            }
            else
            {
                invitationResult = ServiceResult<List<ElectionInvitation>>.Ok(
                    new List<ElectionInvitation>());
            }
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
                IsVisible = request.IsVisible,
                AudienceGroupsSnapshot = (request.IsClosed && request.InvitedAudienceGroups.Count > 0)
                    ? JsonSerializer.Serialize(request.InvitedAudienceGroups)
                    : null,
                StartsAt = request.StartsAt,
                EndsAt = request.EndsAt,
                Invitations = invitationResult.Data!
            };
            election.Questions = BuildQuestions(questions, election.Id);

            foreach (var invitation in election.Invitations)
                invitation.ElectionId = election.Id;

            // Claiming has to follow the insert (ElectionId is a foreign key), but a failure in
            // between would leave the ballot pointing at drafts the sweep deletes a day later.
            await using var transaction = await _elections.BeginTransactionAsync();

            await _elections.AddAsync(election);
            await _auditLogs.AddAsync(new AuditLog
            {
                UserId = userId,
                ElectionId = election.Id,
                Action = AuditAction.ElectionCreated.ToDbValue()
            });
            await _elections.SaveChangesAsync();

            if (!await _images.ClaimAsync(imageIds, election.Id))
                return ServiceResult<ElectionDto>.Fail(ErrorCode.InvalidImageReference);

            await transaction.CommitAsync();

            _logger.LogInformation("Election created: {Title} (ElectionId: {ElectionId}, CreatedBy: {UserId})", election.Title, election.Id, userId);

            foreach (var invitation in election.Invitations)
            {
                if (invitation.UserId.HasValue)
                {
                    var notification = new Notification
                    {
                        UserId = invitation.UserId.Value,
                        Message = NotificationMessages.InvitationNotification(election.Title),
                        Type = NotificationMessages.InvitationType,
                        ReferenceId = election.Id
                    };
                    await _notifications.AddAsync(notification);
                }

                if (!string.IsNullOrEmpty(invitation.Email))
                {
                    string emailMessage = invitation.UserId.HasValue
                        ? NotificationMessages.InvitationEmailRegistered(election.Title)
                        : NotificationMessages.InvitationEmailUnregistered(election.Title);

                    _ = Task.Run(() => _emailService.SendEmailAsync(
                        invitation.Email,
                        NotificationMessages.ElectionInvitationSubject,
                        emailMessage
                    ));
                }
            }

            return ServiceResult<ElectionDto>.Ok(MapToDto(election));
        }

        public async Task<ServiceResult<ElectionDto>> UpdateAsync(Guid id, UpdateElectionRequest request, Guid userId)
        {
            if (!TryParseType(request.Type, out var type))
                return ServiceResult<ElectionDto>.Fail(ErrorCode.InvalidElectionType);

            var questions = NormalizeQuestions(request);
            if (!QuestionsAreValid(questions))
                return ServiceResult<ElectionDto>.Fail(ErrorCode.TooFewOptions);

            if (!RankCountsAreValid(questions))
                return ServiceResult<ElectionDto>.Fail(ErrorCode.InvalidRankCount);

            if (request.EndsAt <= request.StartsAt)
                return ServiceResult<ElectionDto>.Fail(ErrorCode.InvalidDateRange);

            var election = await _elections.GetByIdWithOptionsAsync(id);
            if (election is null)
                return ServiceResult<ElectionDto>.NotFound(ErrorCode.ResourceNotFound);

            if (election.CreatedByUserId != userId)
            {
                _logger.LogWarning("Unauthorized update attempt on ElectionId {ElectionId} by UserId {UserId}", id, userId);
                return ServiceResult<ElectionDto>.Fail(ErrorCode.NotAuthorizedToEdit);
            }

            if (await _votes.HasAnyVotesInElectionAsync(election.Id))
                return ServiceResult<ElectionDto>.Fail(ErrorCode.ElectionHasVotes);

            // After the ownership check, so an outsider gets a permission error rather than a
            // confusing one about images. The election's own id keeps its existing pictures valid.
            var imageIds = CollectImageIds(questions);
            var imagesUsable = await _images.ValidateClaimableAsync(imageIds, userId, electionId: id);
            if (!imagesUsable.Success)
                return ServiceResult<ElectionDto>.Fail(imagesUsable.ErrorCode!.Value);

            // Same reasoning as CreateAsync: the rebuilt ballot and its claims must land together.
            await using var transaction = await _elections.BeginTransactionAsync();

            election.Title = request.Title.Trim();
            election.Description = request.Description;
            election.Question = questions[0].Text.Trim();
            election.Type = type;
            election.IsAnonymous = request.IsAnonymous;
            election.IsClosed = request.IsClosed;
            election.IsVisible = request.IsVisible;
            election.StartsAt = request.StartsAt;
            election.EndsAt = request.EndsAt;

            var existingOptions = election.Options.ToList();
            var existingQuestions = election.Questions.ToList();
            _elections.RemoveOptions(existingOptions);
            _elections.RemoveQuestions(existingQuestions);
            election.Options.Clear();
            election.Questions.Clear();

            // Via the DbSet, not the navigation collection: questions are constructed with a
            // real Id, so EF would treat them as Modified and issue UPDATEs against the rows
            // just deleted above.
            var newQuestions = BuildQuestions(questions, election.Id);
            await _elections.AddQuestionsAsync(newQuestions);

            await _auditLogs.AddAsync(new AuditLog
            {
                UserId = userId,
                ElectionId = election.Id,
                Action = AuditAction.ElectionUpdated.ToDbValue()
            });
            await _elections.SaveChangesAsync();

            if (!await _images.ClaimAsync(imageIds, election.Id))
                return ServiceResult<ElectionDto>.Fail(ErrorCode.InvalidImageReference);

            // Whatever the edit dropped is now unreachable from any ballot, so it goes with it.
            await _images.ReleaseUnreferencedAsync(election.Id, imageIds);

            await transaction.CommitAsync();

            _logger.LogInformation("Election updated: {ElectionId} by UserId {UserId}", election.Id, userId);

            var invitationsForNotification = await _invitations.GetByElectionAsync(election.Id);
            foreach (var invitation in invitationsForNotification)
            {
                if (invitation.UserId.HasValue)
                {
                    var notification = new Notification
                    {
                        UserId = invitation.UserId.Value,
                        Message = NotificationMessages.ElectionUpdatedNotification(election.Title),
                        Type = NotificationMessages.ElectionUpdatedType,
                        ReferenceId = election.Id
                    };
                    await _notifications.AddAsync(notification);
                }

                if (!string.IsNullOrEmpty(invitation.Email))
                {
                    string emailMessage = invitation.UserId.HasValue
                        ? NotificationMessages.ElectionUpdatedEmailRegistered(election.Title)
                        : NotificationMessages.ElectionUpdatedEmailUnregistered(election.Title);

                    _ = Task.Run(() => _emailService.SendEmailAsync(
                        invitation.Email,
                        NotificationMessages.ElectionUpdatedSubject,
                        emailMessage
                    ));
                }
            }

            return ServiceResult<ElectionDto>.Ok(MapToDto(election));
        }

        public async Task<ServiceResult<bool>> DeleteAsync(Guid id, Guid userId)
        {
            var election = await _elections.GetByIdAsync(id);
            if (election is null)
                return ServiceResult<bool>.NotFound(ErrorCode.ResourceNotFound);

            if (election.CreatedByUserId != userId)
            {
                _logger.LogWarning("Unauthorized delete attempt on ElectionId {ElectionId} by UserId {UserId}", id, userId);
                return ServiceResult<bool>.Fail(ErrorCode.NotAuthorizedToDelete);
            }

            // Before the delete, while the title is still readable.
            await _auditLogs.AddAsync(new AuditLog
            {
                UserId = userId,
                ElectionId = null,
                Action = $"{AuditAction.ElectionDeleted.ToDbValue()}:{election.Title}"
            });

            _elections.Remove(election);
            await _elections.SaveChangesAsync();

            _logger.LogInformation("Election deleted: '{Title}' (ElectionId: {ElectionId}) by UserId {UserId}", election.Title, election.Id, userId);

            return ServiceResult<bool>.Ok(true);
        }

        public async Task<ServiceResult<ElectionDto>> PublishElectionAsync(Guid electionId, Guid userId)
        {
            var election = await _elections.GetByIdAsync(electionId);
            if (election is null)
                return ServiceResult<ElectionDto>.NotFound(ErrorCode.ResourceNotFound);

            if (election.CreatedByUserId != userId)
            {
                _logger.LogWarning("Unauthorized publish attempt on ElectionId {ElectionId} by UserId {UserId}", electionId, userId);
                return ServiceResult<ElectionDto>.Fail(ErrorCode.NotAuthorizedToPublish);
            }

            if (election.IsVisible)
                return ServiceResult<ElectionDto>.Fail(ErrorCode.ElectionAlreadyVisible);

            election.IsVisible = true;

            await _auditLogs.AddAsync(new AuditLog
            {
                UserId = userId,
                ElectionId = election.Id,
                Action = AuditAction.ElectionPublished.ToDbValue()
            });
            await _elections.SaveChangesAsync();

            _logger.LogInformation("Election published: {ElectionId} by UserId {UserId}", election.Id, userId);

            return ServiceResult<ElectionDto>.Ok(MapToDto(election));
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

        public async Task<List<InvitationLabelDto>> GetInvitationLabelsAsync(Guid userId)
        {
            var labels = await _labels.GetAllAsync();
            var result = new List<InvitationLabelDto>();

            foreach (var label in labels)
            {
                var assignments = await _labels.GetUsersWithLabelAsync(label.Id);
                var memberIds = assignments
                    .Select(assignment => assignment.UserId)
                    .Where(id => id != userId)
                    .Distinct()
                    .ToList();

                result.Add(new InvitationLabelDto
                {
                    Id = label.Id,
                    Name = label.Name,
                    Category = label.Category,
                    UserCount = memberIds.Count,
                    UserIds = memberIds
                });
            }

            return result;
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

                _logger.LogInformation("{Count} invitation(s) added to ElectionId {ElectionId} by UserId {UserId}", invitationResult.Data.Count, electionId, userId);

                foreach (var invitation in invitationResult.Data)
                {
                    if (invitation.UserId.HasValue)
                    {
                        var notification = new Notification
                        {
                            UserId = invitation.UserId.Value,
                            Message = NotificationMessages.InvitationNotification(election.Title),
                            Type = NotificationMessages.InvitationType,
                            ReferenceId = election.Id
                        };
                        await _notifications.AddAsync(notification);
                    }

                    if (!string.IsNullOrEmpty(invitation.Email))
                    {
                        string emailMessage = invitation.UserId.HasValue
                            ? NotificationMessages.InvitationEmailRegistered(election.Title)
                            : NotificationMessages.InvitationEmailUnregistered(election.Title);

                        _ = Task.Run(() => _emailService.SendEmailAsync(
                            invitation.Email,
                            NotificationMessages.ElectionInvitationSubject,
                            emailMessage
                        ));
                    }
                }
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
            _logger.LogInformation("Invitation {InvitationId} removed from ElectionId {ElectionId} by UserId {UserId}", invitationId, electionId, userId);
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

        /// <summary>
        /// Resolves an OR-of-AND-groups audience rule into the flat set of user IDs to invite.
        /// Side-effect free; each distinct label is fetched exactly once.
        /// </summary>
        private async Task<ServiceResult<List<Guid>>> ExpandAudienceGroupsAsync(
            IEnumerable<Guid> manuallyInvitedUserIds,
            IEnumerable<AudienceGroupDto> audienceGroups)
        {
            var groups = audienceGroups.ToList();

            var referencedLabelIds = groups
                .SelectMany(g => g.Conditions)
                .Select(c => c.LabelId)
                .Distinct()
                .ToList();

            // No conditions means manual user IDs only.
            if (referencedLabelIds.Count == 0)
                return ServiceResult<List<Guid>>.Ok(manuallyInvitedUserIds.Distinct().ToList());

            var existingLabels = await _labels.GetByIdsAsync(referencedLabelIds);
            if (existingLabels.Count != referencedLabelIds.Count)
                return ServiceResult<List<Guid>>.Fail(ErrorCode.LabelNotFound);

            // Once per label: the same label often appears in several groups.
            var labelUserSets = new Dictionary<Guid, HashSet<Guid>>();
            foreach (var labelId in referencedLabelIds)
            {
                var assignments = await _labels.GetUsersWithLabelAsync(labelId);
                labelUserSets[labelId] = assignments.Select(a => a.UserId).ToHashSet();
            }

            var result = manuallyInvitedUserIds.ToHashSet();
            foreach (var group in groups)
            {
                var positiveConditions = group.Conditions.Where(c => !c.IsExcluded).ToList();
                var excludedConditions = group.Conditions.Where(c => c.IsExcluded).ToList();

                HashSet<Guid>? candidates = null;
                foreach (var condition in positiveConditions)
                {
                    var usersWithLabel = labelUserSets[condition.LabelId];
                    if (candidates is null)
                        candidates = new HashSet<Guid>(usersWithLabel);
                    else
                        candidates.IntersectWith(usersWithLabel);
                }

                if (candidates is null || candidates.Count == 0)
                    continue;

                foreach (var exclusion in excludedConditions)
                    candidates.ExceptWith(labelUserSets[exclusion.LabelId]);

                foreach (var uid in candidates)
                    result.Add(uid);
            }

            return ServiceResult<List<Guid>>.Ok(result.ToList());
        }

        private static bool TryParseType(string raw, out ElectionType type)
        {
            type = default;
            return Enum.GetNames<ElectionType>().Any(
                       name => string.Equals(name, raw.Trim(), StringComparison.OrdinalIgnoreCase)) &&
                   Enum.TryParse(raw, ignoreCase: true, out type);
        }

        private static bool TryParseQuestionType(string raw, out QuestionType type)
        {
            type = default;
            return Enum.GetNames<QuestionType>().Any(
                       name => string.Equals(name, raw.Trim(), StringComparison.OrdinalIgnoreCase)) &&
                   Enum.TryParse(raw, ignoreCase: true, out type);
        }

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
                TryParseQuestionType(q.QuestionType, out var questionType) &&
                // A Choice question needs 2+ selectable options; a FreeText question's options
                // are just optional suggestion chips, so none are required.
                (questionType == QuestionType.FreeText ||
                 q.Options.Count(o => !string.IsNullOrWhiteSpace(o.Label)) >= 2));

        // Separate from QuestionsAreValid so the caller can report it distinctly: folded into
        // TooFewOptions it would tell the creator to add options they already have.
        private static bool RankCountsAreValid(IEnumerable<CreateElectionQuestionDto> questions)
            => questions.All(q =>
            {
                if (!q.RequiredRankCount.HasValue)
                    return true;
                if (!TryParseQuestionType(q.QuestionType, out var questionType) || questionType != QuestionType.Ranking)
                    return false;
                var optionCount = q.Options.Count(o => !string.IsNullOrWhiteSpace(o.Label));
                return q.RequiredRankCount.Value >= 1 && q.RequiredRankCount.Value <= optionCount;
            });

        /// <summary>
        /// Every image the ballot will actually reference. The blank-label filter mirrors
        /// <see cref="BuildQuestions"/>: claiming an image for an option that gets dropped would
        /// attach it to nothing.
        /// </summary>
        private static List<Guid> CollectImageIds(IEnumerable<CreateElectionQuestionDto> questions)
            => questions
                .SelectMany(question => question.Options
                    .Where(option => !string.IsNullOrWhiteSpace(option.Label))
                    .Select(option => option.ImageId)
                    .Append(question.ImageId))
                .Where(imageId => imageId.HasValue)
                .Select(imageId => imageId!.Value)
                .Distinct()
                .ToList();

        private static List<ElectionQuestion> BuildQuestions(
            IEnumerable<CreateElectionQuestionDto> questions,
            Guid? electionId = null)
            => questions.Select((question, questionIndex) =>
            {
                // Already validated as parseable by QuestionsAreValid before this runs.
                TryParseQuestionType(question.QuestionType, out var questionType);
                return new ElectionQuestion
                {
                    ElectionId = electionId ?? Guid.Empty,
                    Text = question.Text.Trim(),
                    DisplayOrder = questionIndex,
                    IsRequired = question.IsRequired,
                    AllowMultipleAnswers = question.AllowMultipleAnswers,
                    QuestionType = questionType,
                    AllowOtherOption = questionType == QuestionType.Choice && question.AllowOtherOption,
                    RequiredRankCount = questionType == QuestionType.Ranking ? question.RequiredRankCount : null,
                    ScoringSchemeId = questionType == QuestionType.Ranking ? question.ScoringSchemeId : null,
                    ImageId = question.ImageId,
                    Options = question.Options
                        .Where(option => !string.IsNullOrWhiteSpace(option.Label))
                        .Select(option => new Option
                        {
                            ElectionId = electionId ?? Guid.Empty,
                            Label = option.Label.Trim(),
                            Description = option.Description?.Trim(),
                            ImageId = option.ImageId
                        })
                        .ToList()
                };
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
                    IsRequired = q.IsRequired,
                    AllowMultipleAnswers = q.AllowMultipleAnswers,
                    QuestionType = q.QuestionType.ToString(),
                    AllowOtherOption = q.AllowOtherOption,
                    RequiredRankCount = q.RequiredRankCount,
                    ScoringSchemeId = q.ScoringSchemeId,
                    ImageId = q.ImageId,
                    Options = q.Options.Select(MapOptionToDto).ToList()
                })
                .ToList();

            if (questions.Count == 0)
            {
                questions.Add(new ElectionQuestionDto
                {
                    Id = Guid.Empty,
                    Text = e.Question ?? e.Title,
                    IsRequired = true,
                    QuestionType = QuestionType.Choice.ToString(),
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
                IsVisible = e.IsVisible,
                StartsAt = e.StartsAt,
                EndsAt = e.EndsAt,
                Options = questions[0].Options,
                Questions = questions,
                AudienceGroups = !string.IsNullOrWhiteSpace(e.AudienceGroupsSnapshot)
                    ? JsonSerializer.Deserialize<List<AudienceGroupDto>>(e.AudienceGroupsSnapshot)
                    : null,
                HasUserVoted = false,
                IsExpired = DateTime.UtcNow > e.EndsAt
            };
        }

        private static OptionDto MapOptionToDto(Option option) => new()
        {
            Id = option.Id,
            Label = option.Label,
            Description = option.Description,
            ImageId = option.ImageId
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