using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SiteReportApp.Auth;
using SiteReportApp.Data;
using SiteReportApp.Dtos;
using SiteReportApp.Services;

namespace SiteReportApp.Controllers
{
    [ApiController]
    [Route("api/scorecard")]
    [Authorize]
    public class ScorecardController : ControllerBase
    {
        private readonly ScorecardService _scorecard;
        private readonly ScorecardExcelService _excel;
        private readonly AppDbContext _db;

        public ScorecardController(ScorecardService scorecard, ScorecardExcelService excel, AppDbContext db)
        {
            _scorecard = scorecard;
            _excel = excel;
            _db = db;
        }

        // ---- Schema ----
        // GET /api/scorecard/schema           -> all 20 metric definitions (grouped by category in the UI)
        [HttpGet("schema")]
        public IActionResult GetSchema() => Ok(_scorecard.GetSchema());

        // GET /api/scorecard/schema/{metricKey}
        [HttpGet("schema/{metricKey}")]
        public IActionResult GetMetricSchema(string metricKey)
        {
            var dto = _scorecard.GetMetricSchema(metricKey);
            return dto == null ? NotFound(new { error = $"Unknown metric '{metricKey}'." }) : Ok(dto);
        }

        // ---- Data entry ----
        // GET /api/scorecard/rows?siteId=1&reportPeriodId=5&metricKey=oosRate
        [HttpGet("rows")]
        public async Task<IActionResult> GetRows(
            [FromQuery] int siteId, [FromQuery] int reportPeriodId, [FromQuery] string metricKey)
        {
            if (!User.CanAccessSite(siteId)) return Forbid();
            try
            {
                var rows = await _scorecard.GetRowsAsync(siteId, reportPeriodId, metricKey);
                return Ok(rows);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        // POST /api/scorecard/rows   body: ScorecardSaveDto (replace semantics for that metric)
        [HttpPost("rows")]
        public async Task<IActionResult> SaveRows([FromBody] ScorecardSaveDto dto)
        {
            if (!User.CanAccessSite(dto.SiteId)) return Forbid();
            try
            {
                var saved = await _scorecard.SaveAsync(dto);
                return Ok(new { rowsSaved = saved });
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { error = ex.Message });
            }
        }

        // GET /api/scorecard/status?siteId=1&reportPeriodId=5
        // Row counts per metric — drives the "which sheets are filled" checklist.
        [HttpGet("status")]
        public async Task<IActionResult> GetStatus([FromQuery] int siteId, [FromQuery] int reportPeriodId)
        {
            if (!User.CanAccessSite(siteId)) return Forbid();
            var counts = await _scorecard.GetRowCountsAsync(siteId, reportPeriodId);
            return Ok(counts);
        }

        // ---- Template download ----
        // GET /api/scorecard/template  -> blank .xlsx with one tab per metric
        [HttpGet("template")]
        public IActionResult DownloadTemplate()
        {
            var bytes = _excel.BuildTemplate();
            return File(bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "Monthly_Site_Scorecard_Template.xlsx");
        }

        // ---- Excel import ----
        // POST /api/scorecard/import?siteId=1&reportPeriodId=5  (multipart, field "file")
        [HttpPost("import")]
        [RequestSizeLimit(20_000_000)]
        public async Task<IActionResult> Import(
            [FromQuery] int siteId, [FromQuery] int reportPeriodId, IFormFile file)
        {
            if (!User.CanAccessSite(siteId)) return Forbid();
            if (file == null || file.Length == 0)
                return BadRequest(new { error = "No file uploaded." });
            if (!file.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
                return BadRequest(new { error = "Only .xlsx files are supported." });

            List<ScorecardSaveDto> saves;
            ScorecardImportResultDto result;
            try
            {
                using var stream = file.OpenReadStream();
                (saves, result) = _excel.ParseWorkbook(stream, siteId, reportPeriodId);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = $"Could not read the workbook: {ex.Message}" });
            }

            await using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                foreach (var save in saves)
                    await _scorecard.SaveAsync(save);
                await tx.CommitAsync();
                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                await tx.RollbackAsync();
                return Conflict(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                return StatusCode(500, new { error = $"Import failed and was rolled back: {ex.Message}" });
            }
        }

        // ---- Analytics ----
        // POST /api/scorecard/analytics   body: ScorecardAnalyticsQueryDto
        // Returns flat (site, period, column, value) points the frontend pivots into
        // month-wise, combined-sites, single-month comparison and multi-column views.
        [HttpPost("analytics")]
        public async Task<IActionResult> Analytics([FromBody] ScorecardAnalyticsQueryDto query)
        {
            if (query.FromMonth is < 1 or > 12 || query.ToMonth is < 1 or > 12)
                return BadRequest(new { error = "Month must be between 1 and 12." });
            // Site users can only chart their own site's data.
            if (!User.IsCorporate())
            {
                var ownSiteId = User.GetSiteId();
                if (ownSiteId == null) return Forbid();
                query.SiteIds = new List<int> { ownSiteId.Value };
            }
            try
            {
                var data = await _scorecard.GetAnalyticsAsync(query);
                return Ok(data);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }
    }
}
