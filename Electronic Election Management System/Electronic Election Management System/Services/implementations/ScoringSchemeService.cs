using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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

        public ScoringSchemeService(ElectionDbContext db)
        {
            _db = db;
        }

        public async Task<List<ScoringSchemeDto>> GetSchemesAsync(Guid userId)
        {
            var schemes = await _db.ScoringSchemes
                .Where(s => s.IsPredefined || s.CreatedByUserId == userId)
                .OrderByDescending(s => s.IsPredefined)
                .ThenBy(s => s.Name)
                .ToListAsync();

            return schemes.Select(MapToDto).ToList();
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
