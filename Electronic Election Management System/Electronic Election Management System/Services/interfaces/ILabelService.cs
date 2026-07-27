using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Electronic_Election_Management_System.DTOs;

namespace Electronic_Election_Management_System.Services
{
    public interface ILabelService
    {
        // --- Admin: label management ---

        /// <summary>Returns all labels.</summary>
        Task<List<LabelDto>> GetAllLabelsAsync();

        /// <summary>Creates a new label. Fails if the name is already taken.</summary>
        Task<ServiceResult<LabelDto>> CreateLabelAsync(CreateLabelRequest request);

        /// <summary>Deletes a label and cascades to all user assignments.</summary>
        Task<ServiceResult<bool>> DeleteLabelAsync(Guid id);

        // --- Admin: user–label assignments ---

        /// <summary>Returns all labels assigned to a user (admin view).</summary>
        Task<ServiceResult<List<UserLabelDto>>> GetUserLabelsAsync(Guid userId);

        /// <summary>
        /// Assigns the given labels to a user. Skips already-assigned ones (idempotent).
        /// Returns the full updated label list for that user.
        /// </summary>
        Task<ServiceResult<List<UserLabelDto>>> AssignLabelsToUserAsync(
            Guid userId, AssignLabelsRequest request, Guid adminId);

        /// <summary>Removes a specific label from a user.</summary>
        Task<ServiceResult<bool>> RemoveLabelFromUserAsync(Guid userId, Guid labelId);

        /// <summary>Returns all users that have a given label (for admin segmentation).</summary>
        Task<ServiceResult<List<UserWithLabelDto>>> GetUsersWithLabelAsync(Guid labelId);

        // --- User: read-only view ---

        /// <summary>Returns the labels assigned to the calling user (read-only, for user profile).</summary>
        Task<ServiceResult<List<UserLabelDto>>> GetMyLabelsAsync(Guid userId);
    }
}
