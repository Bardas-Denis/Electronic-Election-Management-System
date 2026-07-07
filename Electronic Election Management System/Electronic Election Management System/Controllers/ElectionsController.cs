using System.Security.Claims;
using Electronic_Election_Management_System.DTOs;
using Electronic_Election_Management_System.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Electronic_Election_Management_System.Controllers
{
    [ApiController]
    [Route("api/voting/elections")]
    [Authorize]
    public class ElectionsController : ControllerBase
    {
        private readonly IElectionService _electionService;

        public ElectionsController(IElectionService electionService)
        {
            _electionService = electionService;
        }

        // GET /api/voting/elections
        [HttpGet]
        public async Task<ActionResult<List<ElectionDto>>> GetAll()
        {
            var elections = await _electionService.GetAllAsync();
            return Ok(elections);
        }

        // GET /api/voting/elections/{id}
        [HttpGet("{id:guid}")]
        public async Task<ActionResult<ElectionDto>> GetById(Guid id)
        {
            var election = await _electionService.GetByIdAsync(id);
            if (election is null)
                return NotFound();
            return Ok(election);
        }

        // POST /api/voting/elections  (doar Admin - CRUD alegeri, Etapa 2)
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ElectionDto>> Create(CreateElectionRequest request)
        {
            var result = await _electionService.CreateAsync(request, GetCurrentUserId());
            if (!result.Success)
                return BadRequest(new { message = result.Error });
            return CreatedAtAction(nameof(GetById), new { id = result.Data!.Id }, result.Data);
        }

        // PUT /api/voting/elections/{id}  (doar Admin)
        [HttpPut("{id:guid}")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ElectionDto>> Update(Guid id, UpdateElectionRequest request)
        {
            var result = await _electionService.UpdateAsync(id, request, GetCurrentUserId());
            if (!result.Success)
                return result.IsNotFound
                    ? NotFound(new { message = result.Error })
                    : BadRequest(new { message = result.Error });
            return Ok(result.Data);
        }

        // DELETE /api/voting/elections/{id}  (doar Admin)
        [HttpDelete("{id:guid}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var result = await _electionService.DeleteAsync(id, GetCurrentUserId());
            if (!result.Success)
                return result.IsNotFound
                    ? NotFound(new { message = result.Error })
                    : BadRequest(new { message = result.Error });
            return NoContent();
        }

        private Guid GetCurrentUserId()
        {
            string? idClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.Parse(idClaim!);
        }
    }
}
