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

        public async Task<ServiceResult<GeographicNodeDto>> CreateChildAsync(
            Guid parentId, string name, string? kind)
        {
            var parent = await _labels.GetByIdAsync(parentId);
            if (parent is null || !LabelCategories.IsGeographic(parent.Category))
                return ServiceResult<GeographicNodeDto>.NotFound(ErrorCode.LabelNotFound);

            var trimmed = name.Trim();
            if (await _labels.NameTakenUnderParentAsync(parentId, trimmed, null))
                return ServiceResult<GeographicNodeDto>.Fail(ErrorCode.LabelNameTakenUnderParent);

            var label = new Label
            {
                Name = trimmed,
                ParentId = parentId,
                // Still derived, never supplied. The category is what the tree is built on, and
                // a hand-typed one would drift from its shape. What the caller names is Kind,
                // which describes the place and carries no structure at all.
                Category = parent.Category == LabelCategories.Country
                    ? LabelCategories.Subdivision
                    : LabelCategories.Locality,
                Kind = Blank(kind),
                // No ISO code: these nodes are local additions, and the filtered unique index on
                // Code accepts any number of nulls.
                Code = null
            };

            await _labels.AddAsync(label);
            await _labels.SaveChangesAsync();

            return ServiceResult<GeographicNodeDto>.Ok(new GeographicNodeDto
            {
                Id = label.Id,
                Name = label.Name,
                Code = label.Code,
                Category = label.Category,
                Kind = label.Kind,
                HasChildren = false
            });
        }

        public async Task<ServiceResult<GeographicNodeDto>> UpdateAsync(Guid id, string name, string? kind)
        {
            var node = await _labels.GetByIdAsync(id);
            if (node is null || !LabelCategories.IsGeographic(node.Category))
                return ServiceResult<GeographicNodeDto>.NotFound(ErrorCode.LabelNotFound);

            var trimmed = name.Trim();

            // Excluding the node itself, so saving a name unchanged is not reported as a clash.
            if (await _labels.NameTakenUnderParentAsync(node.ParentId, trimmed, id))
                return ServiceResult<GeographicNodeDto>.Fail(ErrorCode.LabelNameTakenUnderParent);

            node.Name = trimmed;
            node.Kind = Blank(kind);
            await _labels.SaveChangesAsync();

            return ServiceResult<GeographicNodeDto>.Ok(new GeographicNodeDto
            {
                Id = node.Id,
                Name = node.Name,
                Code = node.Code,
                Category = node.Category,
                Kind = node.Kind,
                HasChildren = await _labels.HasChildrenAsync(id)
            });
        }

        /// <summary>Whitespace and empty both mean "unspecified", and are stored as null.</summary>
        private static string? Blank(string? value) =>
            string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        private static GeographicNodeDto ToDto(GeographicNode node) => new()
        {
            Id = node.Id,
            Name = node.Name,
            Code = node.Code,
            Category = node.Category,
            Kind = node.Kind,
            HasChildren = node.HasChildren
        };
    }
}
