using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SiteReportApp.Auth;
using Microsoft.EntityFrameworkCore;
using SiteReportApp.Data;
using SiteReportApp.Dtos;
using SiteReportApp.Models;
using SiteReportApp.Services;

namespace SiteReportApp.Controllers
{
    [ApiController]
    [Route("api/initiatives")]
    [Authorize]
    public class InitiativesController : ControllerBase
    {
        private readonly DataEntryService _entry;
        private readonly AppDbContext _db;

        public InitiativesController(DataEntryService entry, AppDbContext db)
        {
            _entry = entry;
            _db = db;
        }

        // GET /api/initiatives?siteId=1&reportPeriodId=5&type=LeanLaboratory
        // Added so the frontend can show previously saved rows (and their ids, for delete).
        [HttpGet]
        public async Task<IActionResult> GetForSiteAndPeriod(
            [FromQuery] int siteId, [FromQuery] int reportPeriodId, [FromQuery] string type)
        {
            if (!User.CanAccessSite(siteId)) return Forbid();
            if (!Enum.TryParse<InitiativeType>(type, ignoreCase: true, out var parsedType))
                return BadRequest(new { error = $"Invalid initiative type: '{type}'" });

            var data = await _db.Initiatives
                .Where(i => i.SiteId == siteId && i.ReportPeriodId == reportPeriodId && i.Type == parsedType)
                .OrderBy(i => i.SerialNo)
                .ToListAsync();
            return Ok(data);
        }

        // POST /api/initiatives/bulk
        // body: { siteId, reportPeriodId, type, rows: [...] }
        [HttpPost("bulk")]
        public async Task<IActionResult> SaveBulk([FromBody] InitiativeBulkCreateDto request)
        {
            if (!User.CanAccessSite(request.SiteId)) return Forbid();
            try
            {
                var result = await _entry.SaveInitiativesAsync(request);
                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { error = ex.Message });
            }
        }

        // DELETE /api/initiatives/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var entity = await _db.Initiatives.FindAsync(id);
            if (entity != null && !User.CanAccessSite(entity.SiteId)) return Forbid();
            try
            {
                await _entry.DeleteInitiativeAsync(id);
                return NoContent();
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { error = ex.Message });
            }
        }
    }
}
