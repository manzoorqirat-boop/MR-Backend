using Microsoft.AspNetCore.Mvc;
using SiteReportApp.Dtos;
using SiteReportApp.Services;

namespace SiteReportApp.Controllers
{
    [ApiController]
    [Route("api/initiatives")]
    public class InitiativesController : ControllerBase
    {
        private readonly DataEntryService _entry;

        public InitiativesController(DataEntryService entry)
        {
            _entry = entry;
        }

        // POST /api/initiatives/bulk
        // body: { siteId, reportPeriodId, type, rows: [...] }
        [HttpPost("bulk")]
        public async Task<IActionResult> SaveBulk([FromBody] InitiativeBulkCreateDto request)
        {
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
            await _entry.DeleteInitiativeAsync(id);
            return NoContent();
        }
    }
}
