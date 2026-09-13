using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Electronic_Election_Management_System.DTOs;

namespace Electronic_Election_Management_System.Services
{
    /// <summary>
    /// Read-only browsing of the geographic label tree, one level at a time. Writing to the tree
    /// stays with <see cref="ILabelService"/>, which is admin-only.
    /// </summary>
    public interface IGeographyService
    {
        /// <summary>Every country, ordered by name.</summary>
        Task<List<GeographicNodeDto>> GetCountriesAsync();

        /// <summary>
        /// The direct children of one node. Not-found when the id is unknown or names a label
        /// outside the geographic tree.
        /// </summary>
        Task<ServiceResult<List<GeographicNodeDto>>> GetChildrenAsync(Guid parentId);

        /// <summary>
        /// Adds a node under an existing one. Localities are not seeded, so this is how a country
        /// gets the towns an election actually needs.
        /// </summary>
        /// <param name="kind">
        /// What the place is, in words — "Oraș", "Comună". Optional and descriptive; the node's
        /// category, which the tree relies on, is still derived from the parent.
        /// </param>
        Task<ServiceResult<GeographicNodeDto>> CreateChildAsync(
            Guid parentId, string name, string? kind);

        /// <summary>
        /// Edits a node's name and kind in place. The code and the parent are untouched, so
        /// anything already pointing at this node keeps pointing at it.
        /// </summary>
        Task<ServiceResult<GeographicNodeDto>> UpdateAsync(Guid id, string name, string? kind);

        /// <summary>
        /// Re-parents a node, keeping its name, code and kind. The new parent must sit one level
        /// above the node — a country for a subdivision, a subdivision for a locality — so the
        /// node and everything beneath it keep the categories they already carry.
        /// </summary>
        /// <remarks>
        /// A country is always a root and can never be moved. Moving a node moves its whole
        /// subtree with it, and every user labelled anywhere below changes region as a result.
        /// </remarks>
        Task<ServiceResult<GeographicNodeDto>> MoveAsync(Guid id, Guid newParentId);
    }
}
