using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Electronic_Election_Management_System.Constants;
using Electronic_Election_Management_System.Data.Repositories;
using Electronic_Election_Management_System.DTOs;
using Electronic_Election_Management_System.Models;

namespace Electronic_Election_Management_System.Services
{
    public class LabelService : ILabelService
    {
        private readonly ILabelRepository _labels;
        private readonly IUserRepository _users;
        private readonly ILogger<LabelService> _logger;

        public LabelService(ILabelRepository labels, IUserRepository users, ILogger<LabelService> logger)
        {
            _labels = labels;
            _users = users;
            _logger = logger;
        }

        //Helpers

        private static LabelDto ToDto(Label l) => new()
        {
            Id = l.Id,
            Name = l.Name,
            Category = l.Category,
            CreatedAt = l.CreatedAt
        };

        private static UserLabelDto ToUserLabelDto(UserLabel ul) => new()
        {
            LabelId = ul.LabelId,
            Name = ul.Label.Name,
            Category = ul.Label.Category,
            AssignedBy = ul.AssignedBy,
            AssignedAt = ul.AssignedAt
        };

        //Label management (admin)

        public async Task<List<LabelDto>> GetAllLabelsAsync()
        {
            var labels = await _labels.GetAssignableAsync();
            return labels.Select(ToDto).ToList();
        }

        public async Task<ServiceResult<LabelDto>> CreateLabelAsync(CreateLabelRequest request)
        {
            var trimmedName = request.Name.Trim();

            if (await _labels.ExistsByNameAsync(trimmedName))
                return ServiceResult<LabelDto>.Fail(ErrorCode.LabelNameAlreadyExists);

            var label = new Label
            {
                Name = trimmedName,
                Category = request.Category?.Trim()
            };

            await _labels.AddAsync(label);
            await _labels.SaveChangesAsync();

            _logger.LogInformation("Label created: '{Name}' (LabelId: {LabelId})", label.Name, label.Id);

            return ServiceResult<LabelDto>.Ok(ToDto(label));
        }

        public async Task<ServiceResult<bool>> DeleteLabelAsync(Guid id)
        {
            var label = await _labels.GetByIdAsync(id);
            if (label is null)
                return ServiceResult<bool>.NotFound(ErrorCode.LabelNotFound);

            // The foreign key is Restrict, so deleting a parent would surface as a database
            // failure and a 500. Checking first turns it into an error the client can show.
            if (await _labels.HasChildrenAsync(id))
                return ServiceResult<bool>.Fail(ErrorCode.LabelHasChildren);

            // A user attached to a geographic node moves up to its parent instead of losing the
            // region outright: someone who lived in a commune still lives in the county above it.
            // UserLabels cascades on delete, so without this they would silently end up with no
            // region and only discover it when a regional election refused their vote.
            if (LabelCategories.IsGeographic(label.Category) &&
                await _labels.CountUsersWithLabelAsync(id) > 0)
            {
                if (label.ParentId is null)
                    return ServiceResult<bool>.Fail(ErrorCode.LabelHasUsersAndNoParent);

                var moved = await _labels.ReassignUserLabelsAsync(id, label.ParentId.Value);
                _logger.LogInformation(
                    "Moved {Count} user(s) from label {LabelId} up to its parent {ParentId} before deletion.",
                    moved, id, label.ParentId.Value);
            }

            _labels.Remove(label);
            await _labels.SaveChangesAsync();

            _logger.LogInformation("Label deleted: '{Name}' (LabelId: {LabelId})", label.Name, id);

            return ServiceResult<bool>.Ok(true);
        }

        //User–label assignment (admin)

        public async Task<ServiceResult<List<UserLabelDto>>> GetUserLabelsAsync(Guid userId)
        {
            var user = await _users.GetByIdAsync(userId);
            if (user is null)
                return ServiceResult<List<UserLabelDto>>.NotFound(ErrorCode.ResourceNotFound);

            var rows = await _labels.GetUserLabelsAsync(userId);
            return ServiceResult<List<UserLabelDto>>.Ok(rows.Select(ToUserLabelDto).ToList());
        }

        public async Task<ServiceResult<List<UserLabelDto>>> AssignLabelsToUserAsync(
            Guid userId, AssignLabelsRequest request, Guid adminId)
        {
            var user = await _users.GetByIdAsync(userId);
            if (user is null)
                return ServiceResult<List<UserLabelDto>>.NotFound(ErrorCode.ResourceNotFound);

            //Validate that every requested label id actually exists
            var foundLabels = await _labels.GetByIdsAsync(request.LabelIds);
            if (foundLabels.Count != request.LabelIds.Distinct().Count())
                return ServiceResult<List<UserLabelDto>>.Fail(ErrorCode.LabelNotFound);

            var rows = await _labels.AssignLabelsAsync(userId, request.LabelIds, adminId);
            _logger.LogInformation("{Count} label(s) assigned to UserId {UserId} by AdminId {AdminId}", rows.Count, userId, adminId);
            return ServiceResult<List<UserLabelDto>>.Ok(rows.Select(ToUserLabelDto).ToList());
        }

        public async Task<ServiceResult<bool>> RemoveLabelFromUserAsync(Guid userId, Guid labelId)
        {
            var removed = await _labels.RemoveUserLabelAsync(userId, labelId);
            if (!removed)
                return ServiceResult<bool>.NotFound(ErrorCode.ResourceNotFound);

            await _labels.SaveChangesAsync();
            _logger.LogInformation("Label {LabelId} removed from UserId {UserId}", labelId, userId);
            return ServiceResult<bool>.Ok(true);
        }

        public async Task<ServiceResult<List<UserWithLabelDto>>> GetUsersWithLabelAsync(Guid labelId)
        {
            var label = await _labels.GetByIdAsync(labelId);
            if (label is null)
                return ServiceResult<List<UserWithLabelDto>>.NotFound(ErrorCode.LabelNotFound);

            var rows = await _labels.GetUsersWithLabelAsync(labelId);
            var dtos = rows.Select(ul => new UserWithLabelDto
            {
                UserId = ul.UserId,
                Email = ul.User.Email,
                AssignedAt = ul.AssignedAt
            }).ToList();

            return ServiceResult<List<UserWithLabelDto>>.Ok(dtos);
        }

        //User read-only view

        public async Task<ServiceResult<List<UserLabelDto>>> GetMyLabelsAsync(Guid userId)
        {
            //No user-not-found guard needed here: the calling user authenticated,
            //so they definitely exist. Just return their labels (may be empty list).
            var rows = await _labels.GetUserLabelsAsync(userId);
            return ServiceResult<List<UserLabelDto>>.Ok(rows.Select(ToUserLabelDto).ToList());
        }
    }
}
