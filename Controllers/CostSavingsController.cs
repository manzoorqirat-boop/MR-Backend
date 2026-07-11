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
    [Route("api/cost-savings")]
    [Authorize]
    public class CostSavingsController : ControllerBase
    {
        private readonly DataEntryService _entry;
        private readonly AppDbContext _db;

        public CostSavingsController(DataEntryService entry, AppDbContext db)
        {
            _entry = entry;
            _db = db;
        }

        // GET /api/cost-savings?siteId=1&reportPeriodId=5
        // Added so the frontend can show previously saved rows (and their ids, for delete).
        [HttpGet]
        public async Task<IActionResult> GetForSiteAndPeriod([FromQuery] int siteId, [FromQuery] int reportPeriodId)
        {
            if (!User.CanAccessSite(siteId)) return Forbid();
            var data = await _db.CostSavingInitiatives
                .Where(c => c.SiteId == siteId && c.ReportPeriodId == reportPeriodId)
                .OrderBy(c => c.SerialNo)
                .ToListAsync();
            return Ok(data);
        }

        // POST /api/cost-savings/bulk
        [HttpPost("bulk")]
        public async Task<IActionResult> SaveBulk([FromBody] CostSavingBulkCreateDto request)
        {
            if (!User.CanAccessSite(request.SiteId)) return Forbid();
            try
            {
                var result = await _entry.SaveCostSavingsAsync(request);
                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { error = ex.Message });
            }
        }

        // DELETE /api/cost-savings/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var entity = await _db.CostSavingInitiatives.FindAsync(id);
            if (entity != null && !User.CanAccessSite(entity.SiteId)) return Forbid();
            try
            {
                await _entry.DeleteCostSavingAsync(id);
                return NoContent();
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { error = ex.Message });
            }
        }
    }
}
