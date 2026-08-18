using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Electronic_Election_Management_System.DTOs;

namespace Electronic_Election_Management_System.Services.interfaces
{
    public interface IScoringSchemeService
    {
        Task<List<ScoringSchemeDto>> GetSchemesAsync(Guid userId);
        Task<ScoringSchemeDto> CreateSchemeAsync(CreateScoringSchemeDto dto, Guid userId);
    }
}
