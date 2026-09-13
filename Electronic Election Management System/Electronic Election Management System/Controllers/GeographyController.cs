using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Electronic_Election_Management_System.DTOs;
using Electronic_Election_Management_System.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Electronic_Election_Management_System.Controllers
{
    /// <summary>
    /// Browsing of the geographic label tree. Open to any authenticated user rather than to
    /// admins: a voter filling in their profile picks their country and region from here, and
    /// the tree holds no data that is private to anyone.
    /// </summary>
    [ApiController]
    [Route("api/geography")]
    [Authorize]
    public class GeographyController : ControllerBase
    {
        private readonly IGeographyService _geography;

        public GeographyController(IGeographyService geography)
        {
            _geography = geography;
        }

        /// <summary>Returns every country, ordered by name.</summary>
        [HttpGet("countries")]
        public async Task<ActionResult<List<GeographicNodeDto>>> GetCountries()
        {
            return Ok(await _geography.GetCountriesAsync());
        }

        /// <summary>
        /// Returns the direct children of a node — a country's subdivisions, a subdivision's
        /// localities. One level per request.
        /// </summary>
        [HttpGet("{id:guid}/children")]
        public async Task<ActionResult<List<GeographicNodeDto>>> GetChildren(Guid id)
        {
            var result = await _geography.GetChildrenAsync(id);
            if (!result.Success)
                return NotFound(new { errorCode = result.ErrorCode });

            return Ok(result.Data);
        }
    }
}
