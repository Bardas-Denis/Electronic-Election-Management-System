using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Electronic_Election_Management_System.PluginContracts;
using Electronic_Election_Management_System.Plugins;
using Electronic_Election_Management_System.Data;
using Electronic_Election_Management_System.DTOs;
using Electronic_Election_Management_System.Models;
using Electronic_Election_Management_System.Services.interfaces;
using Microsoft.EntityFrameworkCore;

namespace Electronic_Election_Management_System.Services.implementations
{
    public class ScoringSchemeService : IScoringSchemeService
    {
        private readonly ElectionDbContext _db;
        private readonly IPluginHost _plugins;

        public ScoringSchemeService(ElectionDbContext db, IPluginHost plugins)
        {
            _db = db;
            _plugins = plugins;
        }

        public async Task<List<ScoringSchemeDto>> GetSchemesAsync(Guid userId)
        {
            var schemes = await _db.ScoringSchemes
                .Where(s => s.IsPredefined || s.CreatedByUserId == userId)
                .OrderByDescending(s => s.IsPredefined)
                .ThenBy(s => s.Name)
                .ToListAsync();

            // The plugin folder is the source of truth for what a plugin-backed scheme is:
            // once its assembly is gone the scheme stops being offered. The row itself survives,
            // so elections already scored by it keep resolving instead of losing their results.
            return schemes
                .Where(s => string.IsNullOrEmpty(s.PluginKey)
                            || _plugins.TryGet<IScoringPlugin>(s.PluginKey, out _))
                .Select(MapToDto)
                .ToList();
        }

        public async Task<ScoringSchemeDto> CreateSchemeAsync(CreateScoringSchemeDto dto, Guid userId)
        {
            var scheme = new ScoringScheme
            {
                Name = dto.Name.Trim(),
                Points = dto.Points ?? new List<int>(),
                IsLinear = false,
                IsPredefined = false,
                CreatedByUserId = userId
            };

            _db.ScoringSchemes.Add(scheme);
            await _db.SaveChangesAsync();

            return MapToDto(scheme);
        }

        private static ScoringSchemeDto MapToDto(ScoringScheme s) => new()
        {
            Id = s.Id,
            Name = s.Name,
            Points = s.Points ?? new List<int>(),
            IsLinear = s.IsLinear,
            IsPredefined = s.IsPredefined,
            PluginKey = s.PluginKey
        };
    }
}
