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

        public LabelService(ILabelRepository labels, IUserRepository users)
        {
            _labels = labels;
            _users = users;
        }

        // ── Helpers ──────────────────────────────────────────────────────────

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

        // ── Label management (admin) ──────────────────────────────────────────

        public async Task<List<LabelDto>> GetAllLabelsAsync()
        {
            var labels = await _labels.GetAllAsync();
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

            return ServiceResult<LabelDto>.Ok(ToDto(label));
        }

        public async Task<ServiceResult<bool>> DeleteLabelAsync(Guid id)
        {
            var label = await _labels.GetByIdAsync(id);
            if (label is null)
                return ServiceResult<bool>.NotFound(ErrorCode.LabelNotFound);

            _labels.Remove(label);
            await _labels.SaveChangesAsync();

            return ServiceResult<bool>.Ok(true);
        }

        // ── User–label assignment (admin) ─────────────────────────────────────

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

            // Validate that every requested label id actually exists
            var foundLabels = await _labels.GetByIdsAsync(request.LabelIds);
            if (foundLabels.Count != request.LabelIds.Distinct().Count())
                return ServiceResult<List<UserLabelDto>>.Fail(ErrorCode.LabelNotFound);

            var rows = await _labels.AssignLabelsAsync(userId, request.LabelIds, adminId);
            return ServiceResult<List<UserLabelDto>>.Ok(rows.Select(ToUserLabelDto).ToList());
        }

        public async Task<ServiceResult<bool>> RemoveLabelFromUserAsync(Guid userId, Guid labelId)
        {
            var removed = await _labels.RemoveUserLabelAsync(userId, labelId);
            if (!removed)
                return ServiceResult<bool>.NotFound(ErrorCode.ResourceNotFound);

            await _labels.SaveChangesAsync();
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

        // ── User read-only view ───────────────────────────────────────────────

        public async Task<ServiceResult<List<UserLabelDto>>> GetMyLabelsAsync(Guid userId)
        {
            // No user-not-found guard needed here: the calling user authenticated,
            // so they definitely exist. Just return their labels (may be empty list).
            var rows = await _labels.GetUserLabelsAsync(userId);
            return ServiceResult<List<UserLabelDto>>.Ok(rows.Select(ToUserLabelDto).ToList());
        }
    }
}
