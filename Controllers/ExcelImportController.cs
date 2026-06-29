using Microsoft.AspNetCore.Mvc;
using SiteReportApp.Services;

namespace SiteReportApp.Controllers
{
    [ApiController]
    [Route("api/excel-import")]
    public class ExcelImportController : ControllerBase
    {
        private readonly ExcelImportService _excelImport;
        private readonly DataEntryService _entry;

        public ExcelImportController(ExcelImportService excelImport, DataEntryService entry)
        {
            _excelImport = excelImport;
            _entry = entry;
        }

        // POST /api/excel-import?siteId=1&reportPeriodId=5
        // multipart/form-data with a single file field named "file"
        // Parses the uploaded workbook (matching the 7-sheet template) and saves all rows.
        [HttpPost]
        [RequestSizeLimit(20_000_000)] // 20 MB cap, plenty for these sheets
        public async Task<IActionResult> Import(
            [FromQuery] int siteId,
            [FromQuery] int reportPeriodId,
            IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { error = "No file uploaded." });

            if (!file.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
                return BadRequest(new { error = "Only .xlsx files are supported." });

            using var stream = file.OpenReadStream();
            var parsed = _excelImport.ParseWorkbook(stream, siteId, reportPeriodId);

            var summary = new
            {
                training = parsed.Training != null
                    ? await _entry.SaveTrainingAsync(parsed.Training)
                    : null,
                initiatives = new List<object>(),
                costSavings = parsed.CostSavings != null
                    ? await _entry.SaveCostSavingsAsync(parsed.CostSavings)
                    : null,
                warnings = parsed.Warnings
            };

            var initiativeResults = new List<object>();
            foreach (var initiativeDto in parsed.Initiatives)
            {
                var res = await _entry.SaveInitiativesAsync(initiativeDto);
                initiativeResults.Add(new { type = initiativeDto.Type, result = res });
            }

            return Ok(new
            {
                training = summary.training,
                initiatives = initiativeResults,
                costSavings = summary.costSavings,
                warnings = summary.warnings
            });
        }
    }
}
