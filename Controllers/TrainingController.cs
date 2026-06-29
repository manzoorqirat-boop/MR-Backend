using Microsoft.AspNetCore.Mvc;
using SiteReportApp.Dtos;
using SiteReportApp.Services;

namespace SiteReportApp.Controllers
{
    [ApiController]
    [Route("api/training")]
    public class TrainingController : ControllerBase
    {
        private readonly DataEntryService _entry;

        public TrainingController(DataEntryService entry)
        {
            _entry = entry;
        }

        // POST /api/training/bulk
        [HttpPost("bulk")]
        public async Task<IActionResult> SaveBulk([FromBody] TrainingBulkCreateDto request)
        {
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
            await _entry.DeleteTrainingAsync(id);
            return NoContent();
        }
    }
}
