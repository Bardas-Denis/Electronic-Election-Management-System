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
    public class ResidenceLabelSync : IResidenceLabelSync
    {
        private readonly ILabelRepository _labels;

        public ResidenceLabelSync(ILabelRepository labels)
        {
            _labels = labels;
        }

        public async Task SyncAsync(Guid userId, UserDetails details)
        {
            var wanted = await ResolveChainAsync(details);

            var held = (await _labels.GetUserLabelsAsync(userId))
                .Where(ul => LabelCategories.IsGeographic(ul.Label.Category))
                .Select(ul => ul.LabelId)
                .ToHashSet();

            foreach (var stale in held.Where(id => !wanted.Contains(id)))
                await _labels.RemoveUserLabelAsync(userId, stale);

            // The user is recorded as the assigner: the label follows from their own profile, and
            // there is no administrator behind it to name.
            var missing = wanted.Where(id => !held.Contains(id)).ToList();
            if (missing.Count > 0)
                await _labels.AssignLabelsAsync(userId, missing, userId);   // also saves the removals
            else
                await _labels.SaveChangesAsync();
        }

        /// <summary>The ids of every level that holds, from the country down.</summary>
        private async Task<List<Guid>> ResolveChainAsync(UserDetails details)
        {
            var chain = new List<Guid>();

            var country = await NodeAsync(details.ResidenceCountry, LabelCategories.Country, null);
            if (country is null)
                return chain;
            chain.Add(country.Id);

            var county = await NodeAsync(
                details.ResidenceCountyCode, LabelCategories.Subdivision, country.Id);
            if (county is null)
                return chain;
            chain.Add(county.Id);

            var city = await LocalityAsync(county.Id, details.ResidenceCity);
            if (city is not null)
                chain.Add(city.Value);

            return chain;
        }

        /// <summary>
        /// The node with this code, provided it is at the expected level and under the expected
        /// parent. The parent check is what keeps a county from being claimed under a country it
        /// does not belong to.
        /// </summary>
        private async Task<Label?> NodeAsync(string? code, string category, Guid? parentId)
        {
            if (string.IsNullOrWhiteSpace(code))
                return null;

            var node = await _labels.GetByCodeAsync(code);
            return node is not null && node.Category == category && node.ParentId == parentId
                ? node
                : null;
        }

        /// <summary>
        /// Finds the city under the county by its folded spelling, or adds it. Returns null when
        /// there is no city, or when the name is longer than a label may be.
        /// </summary>
        private async Task<Guid?> LocalityAsync(Guid countyId, string? typed)
        {
            if (string.IsNullOrWhiteSpace(typed))
                return null;

            var name = typed.Trim();
            if (name.Length > ValidationRules.LabelNameMaxLength)
                return null;

            var key = GeographicNames.Key(name);
            var existing = (await _labels.GetChildrenAsync(countyId))
                .FirstOrDefault(n => n.Category == LabelCategories.Locality
                                     && GeographicNames.Key(n.Name) == key);
            if (existing is not null)
                return existing.Id;

            // Left pending: AssignLabelsAsync saves it together with the link to it, so a failure
            // cannot leave a city behind that nobody lives in.
            var locality = new Label
            {
                Name = name,
                ParentId = countyId,
                Category = LabelCategories.Locality
            };
            await _labels.AddAsync(locality);
            return locality.Id;
        }
    }
}
