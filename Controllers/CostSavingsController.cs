using Microsoft.AspNetCore.Mvc;
using SiteReportApp.Dtos;
using SiteReportApp.Services;

namespace SiteReportApp.Controllers
{
    [ApiController]
    [Route("api/cost-savings")]
    public class CostSavingsController : ControllerBase
    {
        private readonly DataEntryService _entry;

        public CostSavingsController(DataEntryService entry)
        {
            _entry = entry;
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
            await _entry.DeleteCostSavingAsync(id);
            return NoContent();
        }
    }
}
