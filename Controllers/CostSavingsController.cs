using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SiteReportApp.Data;
using SiteReportApp.Dtos;
using SiteReportApp.Services;

namespace SiteReportApp.Controllers
{
    [ApiController]
    [Route("api/cost-savings")]
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
