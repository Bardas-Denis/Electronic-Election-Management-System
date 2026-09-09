using System;
using System.Threading.Tasks;
using Electronic_Election_Management_System.DTOs;
using Electronic_Election_Management_System.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Electronic_Election_Management_System.Controllers
{
    /// <summary>
    /// Curating the geographic tree. Separate from <see cref="GeographyController"/> on purpose:
    /// reading the tree is open to every signed-in user because a voter picks their region from
    /// it, while changing it is an administrator's job.
    /// <para>
    /// Deleting a node stays on the labels endpoint, so there is only one delete path and no way
    /// to bypass the rule that moves a node's users up to its parent.
    /// </para>
    /// </summary>
    [ApiController]
    [Route("api/admin/geography")]
    [Authorize(Roles = "Admin")]
    public class GeographyAdminController : ControllerBase
    {
        private readonly IGeographyService _geography;

        public GeographyAdminController(IGeographyService geography)
        {
            _geography = geography;
        }

        /// <summary>Adds a node under an existing one.</summary>
        [HttpPost]
        public async Task<ActionResult<GeographicNodeDto>> Create(CreateGeographicNodeRequest request)
        {
            var result = await _geography.CreateChildAsync(
                request.ParentId, request.Name, request.Kind);

            if (result.IsNotFound)
                return NotFound(new { errorCode = result.ErrorCode });
            if (!result.Success)
                return BadRequest(new { errorCode = result.ErrorCode });

            return Ok(result.Data);
        }

        /// <summary>Edits a node's name and kind, leaving its code and its place in the tree alone.</summary>
        [HttpPut("{id:guid}")]
        public async Task<ActionResult<GeographicNodeDto>> Update(Guid id, UpdateGeographicNodeRequest request)
        {
            var result = await _geography.UpdateAsync(id, request.Name, request.Kind);

            if (result.IsNotFound)
                return NotFound(new { errorCode = result.ErrorCode });
            if (!result.Success)
                return BadRequest(new { errorCode = result.ErrorCode });

            return Ok(result.Data);
        }
    }
}
