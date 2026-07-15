using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SiteReportApp.Auth;
using SiteReportApp.Data;
using SiteReportApp.Models;

namespace SiteReportApp.Controllers
{
    public class QaItRowDto
    {
        public string EquipmentName { get; set; } = string.Empty;
        public string EquipmentCode { get; set; } = string.Empty;
        public string SoftwareNameVersion { get; set; } = string.Empty;
        public string DepartmentArea { get; set; } = string.Empty;
        public string SystemCategory { get; set; } = string.Empty;
        public string InitialQualificationDate { get; set; } = "";
        public string LastPeriodicReviewDate { get; set; } = "";
        public string NextPlannedDue { get; set; } = "";
        public string DueJustification { get; set; } = "";
        public string ActualDoneOn { get; set; } = "";
        public string ActualDoneBy { get; set; } = "";
    }

    public class QaItSaveDto
    {
        public int SiteId { get; set; }
        public int Year { get; set; }
        public string Version { get; set; } = string.Empty;
        public List<QaItRowDto> Rows { get; set; } = new();
    }

    // QA-IT Compliance Activities — periodic review register for computerized
    // systems, one register per site per year. Saved as a whole document
    // (version + rows), matching how the paper form is maintained.
    [ApiController]
    [Route("api/qa-it/periodic-reviews")]
    [Authorize]
    public class QaItComplianceController : ControllerBase
    {
        private readonly AppDbContext _db;

        public QaItComplianceController(AppDbContext db)
        {
            _db = db;
        }

        // GET /api/qa-it/periodic-reviews?siteId=1&year=2026
        [HttpGet]
        public async Task<IActionResult> Get([FromQuery] int siteId, [FromQuery] int year)
        {
            if (!User.CanAccessSite(siteId)) return Forbid();

            var register = await _db.QaItRegisters.AsNoTracking()
                .FirstOrDefaultAsync(r => r.SiteId == siteId && r.Year == year);

            var rows = await _db.QaItPeriodicReviews.AsNoTracking()
                .Where(r => r.SiteId == siteId && r.Year == year)
                .OrderBy(r => r.SerialNo)
                .ToListAsync();

            return Ok(new
            {
                version = register?.Version ?? "",
                updatedBy = register?.UpdatedBy,
                updatedAtUtc = register?.UpdatedAtUtc,
                rows = rows.Select(r => new
                {
                    r.Id, r.SerialNo, r.EquipmentName, r.EquipmentCode, r.SoftwareNameVersion, r.DepartmentArea,
                    r.SystemCategory, r.InitialQualificationDate, r.LastPeriodicReviewDate,
                    r.NextPlannedDue, r.DueJustification, r.ActualDoneOn, r.ActualDoneBy
                })
            });
        }

        // POST /api/qa-it/periodic-reviews/save — replaces the whole register
        // (version + rows) for the site+year, like saving the document.
        [HttpPost("save")]
        public async Task<IActionResult> Save([FromBody] QaItSaveDto dto)
        {
            if (!User.CanAccessSite(dto.SiteId)) return Forbid();
            if (dto.Year < 2000 || dto.Year > 2100)
                return BadRequest(new { error = "Year must be between 2000 and 2100." });
            if (!await _db.Sites.AnyAsync(s => s.Id == dto.SiteId))
                return BadRequest(new { error = "Site does not exist." });

            // Keep only rows that have at least a system name.
            var rows = dto.Rows
                .Where(r => !string.IsNullOrWhiteSpace(r.EquipmentName))
                .ToList();

            // ---- Frequency rule enforcement ----
            // Auto planned month = (Last Periodic Review date, else Initial
            // Qualification date) + the category's frequency. If a row's
            // NextPlannedDue deviates from that computed month, a justification
            // is mandatory. Enforced here so the rule can't be bypassed.
            var freqMap = await _db.MasterListItems
                .Where(i => i.ListKey == MasterListKeys.SystemCategory && i.FrequencyYears != null)
                .ToDictionaryAsync(i => i.Value, i => i.FrequencyYears!.Value);

            var violations = new List<string>();
            for (var idx = 0; idx < rows.Count; idx++)
            {
                var r = rows[idx];
                var auto = ComputeAutoDue(r, freqMap);
                if (auto == null) continue;                       // rule not computable -> no enforcement
                if (r.NextPlannedDue == auto)
                {
                    r.DueJustification = "";                       // auto-accepted: no stale justification kept
                    continue;
                }
                if (string.IsNullOrWhiteSpace(r.DueJustification))
                    violations.Add($"Row {idx + 1} ({r.EquipmentCode}): planned month {r.NextPlannedDue} differs from the frequency-based {auto} — justification required.");
            }
            if (violations.Count > 0)
                return Conflict(new { error = string.Join(" ", violations) });

            await using var tx = await _db.Database.BeginTransactionAsync();

            var register = await _db.QaItRegisters
                .FirstOrDefaultAsync(r => r.SiteId == dto.SiteId && r.Year == dto.Year);
            if (register == null)
            {
                register = new QaItRegister { SiteId = dto.SiteId, Year = dto.Year };
                _db.QaItRegisters.Add(register);
            }
            register.Version = dto.Version?.Trim() ?? "";
            register.UpdatedBy = User.GetDisplayName();
            register.UpdatedAtUtc = DateTime.UtcNow;

            var existing = await _db.QaItPeriodicReviews
                .Where(r => r.SiteId == dto.SiteId && r.Year == dto.Year)
                .ToListAsync();
            _db.QaItPeriodicReviews.RemoveRange(existing);

            var serial = 1;
            foreach (var r in rows)
            {
                _db.QaItPeriodicReviews.Add(new QaItPeriodicReview
                {
                    SiteId = dto.SiteId,
                    Year = dto.Year,
                    SerialNo = serial++,
                    EquipmentName = r.EquipmentName.Trim(),
                    EquipmentCode = r.EquipmentCode?.Trim() ?? "",
                    SoftwareNameVersion = r.SoftwareNameVersion?.Trim() ?? "",
                    DepartmentArea = r.DepartmentArea?.Trim() ?? "",
                    SystemCategory = r.SystemCategory?.Trim() ?? "",
                    InitialQualificationDate = r.InitialQualificationDate?.Trim() ?? "",
                    LastPeriodicReviewDate = r.LastPeriodicReviewDate?.Trim() ?? "",
                    NextPlannedDue = r.NextPlannedDue?.Trim() ?? "",
                    DueJustification = r.DueJustification?.Trim() ?? "",
                    ActualDoneOn = r.ActualDoneOn?.Trim() ?? "",
                    ActualDoneBy = r.ActualDoneBy?.Trim() ?? ""
                });
            }

            await _db.SaveChangesAsync();
            await tx.CommitAsync();

            return Ok(new { saved = rows.Count });
        }

        // Auto planned month per the frequency table. Returns null when the
        // rule cannot be computed (no base date, or category without frequency).
        internal static string? ComputeAutoDue(QaItRowDto r, Dictionary<string, int> freqMap)
        {
            if (string.IsNullOrWhiteSpace(r.SystemCategory)) return null;
            if (!freqMap.TryGetValue(r.SystemCategory, out var years)) return null;
            var baseRaw = !string.IsNullOrWhiteSpace(r.LastPeriodicReviewDate)
                ? r.LastPeriodicReviewDate
                : r.InitialQualificationDate;
            if (!DateTime.TryParseExact(baseRaw, "yyyy-MM-dd",
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out var baseDate))
                return null;
            return baseDate.AddYears(years).ToString("yyyy-MM");
        }
    }
}
