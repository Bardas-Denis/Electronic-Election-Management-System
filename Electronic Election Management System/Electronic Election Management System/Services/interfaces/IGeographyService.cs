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
        Task<ServiceResult<GeographicNodeDto>> CreateChildAsync(Guid parentId, string name);

        /// <summary>
        /// Renames a node in place. The code is untouched, so anything already pointing at this
        /// node keeps pointing at it.
        /// </summary>
        Task<ServiceResult<GeographicNodeDto>> RenameAsync(Guid id, string name);
    }
}
