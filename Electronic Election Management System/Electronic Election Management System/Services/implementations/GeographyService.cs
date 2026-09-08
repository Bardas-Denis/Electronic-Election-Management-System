using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Electronic_Election_Management_System.Constants;
using Electronic_Election_Management_System.Data.Repositories;
using Electronic_Election_Management_System.DTOs;

namespace Electronic_Election_Management_System.Services
{
    public class GeographyService : IGeographyService
    {
        private readonly ILabelRepository _labels;

        public GeographyService(ILabelRepository labels)
        {
            _labels = labels;
        }

        public async Task<List<GeographicNodeDto>> GetCountriesAsync()
        {
            var countries = await _labels.GetCountriesAsync();
            return countries.Select(ToDto).ToList();
        }

        public async Task<ServiceResult<List<GeographicNodeDto>>> GetChildrenAsync(Guid parentId)
        {
            var parent = await _labels.GetByIdAsync(parentId);

            // Not-found rather than an empty list: an unknown id and a childless node are
            // different answers, and a caller that cannot tell them apart shows an empty picker
            // without ever learning why. Rejecting non-geographic labels also stops this
            // endpoint from being used to probe which label ids exist.
            if (parent is null || !LabelCategories.IsGeographic(parent.Category))
                return ServiceResult<List<GeographicNodeDto>>.NotFound(ErrorCode.LabelNotFound);

            var children = await _labels.GetChildrenAsync(parentId);
            return ServiceResult<List<GeographicNodeDto>>.Ok(children.Select(ToDto).ToList());
        }

        private static GeographicNodeDto ToDto(GeographicNode node) => new()
        {
            Id = node.Id,
            Name = node.Name,
            Code = node.Code,
            Category = node.Category,
            HasChildren = node.HasChildren
        };
    }
}
