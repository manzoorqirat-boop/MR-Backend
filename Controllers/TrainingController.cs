using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SiteReportApp.Auth;
using Microsoft.EntityFrameworkCore;
using SiteReportApp.Data;
using SiteReportApp.Dtos;
using SiteReportApp.Services;

namespace SiteReportApp.Controllers
{
    [ApiController]
    [Route("api/training")]
    [Authorize]
    public class TrainingController : ControllerBase
    {
        private readonly DataEntryService _entry;
        private readonly AppDbContext _db;

        public TrainingController(DataEntryService entry, AppDbContext db)
        {
            _entry = entry;
            _db = db;
        }

        // GET /api/training?siteId=1&reportPeriodId=5
        // Added so the frontend can show previously saved rows (and their ids, for delete).
        [HttpGet]
        public async Task<IActionResult> GetForSiteAndPeriod([FromQuery] int siteId, [FromQuery] int reportPeriodId)
        {
            if (!User.CanAccessSite(siteId)) return Forbid();
            var data = await _db.TrainingRecords
                .Where(t => t.SiteId == siteId && t.ReportPeriodId == reportPeriodId)
                .OrderBy(t => t.SerialNo)
                .ToListAsync();
            return Ok(data);
        }

        // POST /api/training/bulk
        [HttpPost("bulk")]
        public async Task<IActionResult> SaveBulk([FromBody] TrainingBulkCreateDto request)
        {
            if (!User.CanAccessSite(request.SiteId)) return Forbid();
            try
            {
                var result = await _entry.SaveTrainingAsync(request);
                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { error = ex.Message });
            }
        }

        // DELETE /api/training/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var entity = await _db.TrainingRecords.FindAsync(id);
            if (entity != null && !User.CanAccessSite(entity.SiteId)) return Forbid();
            try
            {
                await _entry.DeleteTrainingAsync(id);
                return NoContent();
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { error = ex.Message });
            }
        }
    }
}
