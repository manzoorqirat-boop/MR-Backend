using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SiteReportApp.Auth;
using Microsoft.EntityFrameworkCore;
using SiteReportApp.Data;
using SiteReportApp.Services;

namespace SiteReportApp.Controllers
{
    [ApiController]
    [Route("api/excel-import")]
    [Authorize]
    public class ExcelImportController : ControllerBase
    {
        private readonly ExcelImportService _excelImport;
        private readonly DataEntryService _entry;
        private readonly AppDbContext _db;

        public ExcelImportController(ExcelImportService excelImport, DataEntryService entry, AppDbContext db)
        {
            _excelImport = excelImport;
            _entry = entry;
            _db = db;
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
            if (!User.CanAccessSite(siteId)) return Forbid();
            if (file == null || file.Length == 0)
                return BadRequest(new { error = "No file uploaded." });

            if (!file.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
                return BadRequest(new { error = "Only .xlsx files are supported." });

            ExcelImportService.ParsedImport parsed;
            try
            {
                using var stream = file.OpenReadStream();
                parsed = _excelImport.ParseWorkbook(stream, siteId, reportPeriodId);
            }
            catch (Exception ex)
            {
                // Malformed/corrupt workbook, unexpected cell types, etc.
                return BadRequest(new { error = $"Could not read the workbook: {ex.Message}" });
            }

            // Save everything inside one transaction: if any sheet fails (e.g. the period
            // is locked, or a foreign key is invalid) nothing is persisted, so a re-import
            // after fixing the issue won't leave half the workbook behind.
            await using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                var trainingResult = parsed.Training != null
                    ? await _entry.SaveTrainingAsync(parsed.Training)
                    : null;

                var initiativeResults = new List<object>();
                foreach (var initiativeDto in parsed.Initiatives)
                {
                    var res = await _entry.SaveInitiativesAsync(initiativeDto);
                    initiativeResults.Add(new { type = initiativeDto.Type, result = res });
                }

                var costSavingsResult = parsed.CostSavings != null
                    ? await _entry.SaveCostSavingsAsync(parsed.CostSavings)
                    : null;

                await tx.CommitAsync();

                return Ok(new
                {
                    training = trainingResult,
                    initiatives = initiativeResults,
                    costSavings = costSavingsResult,
                    warnings = parsed.Warnings
                });
            }
            catch (InvalidOperationException ex)
            {
                // Thrown by DataEntryService when the report period is locked / not found.
                await tx.RollbackAsync();
                return Conflict(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                return StatusCode(500, new { error = $"Import failed and was rolled back: {ex.Message}" });
            }
        }
    }
}
