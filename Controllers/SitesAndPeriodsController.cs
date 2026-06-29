using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SiteReportApp.Data;
using SiteReportApp.Dtos;
using SiteReportApp.Models;
using SiteReportApp.Services;

namespace SiteReportApp.Controllers
{
    [ApiController]
    [Route("api/sites")]
    public class SitesController : ControllerBase
    {
        private readonly AppDbContext _db;

        public SitesController(AppDbContext db)
        {
            _db = db;
        }

        // GET /api/sites
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var sites = await _db.Sites.Where(s => s.IsActive).OrderBy(s => s.Name).ToListAsync();
            return Ok(sites);
        }

        // POST /api/sites   body: { name, code }
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] Site site)
        {
            _db.Sites.Add(site);
            await _db.SaveChangesAsync();
            return CreatedAtAction(nameof(GetAll), new { id = site.Id }, site);
        }
    }

    [ApiController]
    [Route("api/report-periods")]
    public class ReportPeriodsController : ControllerBase
    {
        private readonly AppDbContext _db;

        public ReportPeriodsController(AppDbContext db)
        {
            _db = db;
        }

        // GET /api/report-periods
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var periods = await _db.ReportPeriods
                .OrderByDescending(p => p.Year).ThenByDescending(p => p.Month)
                .ToListAsync();
            return Ok(periods);
        }

        // POST /api/report-periods   body: { month, year }
        // Idempotent-ish: returns existing period if month/year already exists
        [HttpPost]
        public async Task<IActionResult> CreateOrGet([FromBody] ReportPeriod period)
        {
            var existing = await _db.ReportPeriods
                .FirstOrDefaultAsync(p => p.Month == period.Month && p.Year == period.Year);
            if (existing != null) return Ok(existing);

            period.Status = ReportPeriodStatus.Open;
            _db.ReportPeriods.Add(period);
            await _db.SaveChangesAsync();
            return CreatedAtAction(nameof(GetAll), new { id = period.Id }, period);
        }

        // PATCH /api/report-periods/5/lock — head office locks the month once finalized
        [HttpPatch("{id}/lock")]
        public async Task<IActionResult> Lock(int id)
        {
            var period = await _db.ReportPeriods.FindAsync(id);
            if (period == null) return NotFound();
            period.Status = ReportPeriodStatus.Locked;
            await _db.SaveChangesAsync();
            return Ok(period);
        }
    }

    [ApiController]
    [Route("api/site-submissions")]
    public class SiteSubmissionsController : ControllerBase
    {
        private readonly DataEntryService _entry;
        private readonly AppDbContext _db;

        public SiteSubmissionsController(DataEntryService entry, AppDbContext db)
        {
            _entry = entry;
            _db = db;
        }

        // GET /api/site-submissions?reportPeriodId=5
        [HttpGet]
        public async Task<IActionResult> GetForPeriod([FromQuery] int reportPeriodId)
        {
            var data = await _db.SiteSubmissions
                .Where(ss => ss.ReportPeriodId == reportPeriodId)
                .Include(ss => ss.Site)
                .ToListAsync();
            return Ok(data);
        }

        // POST /api/site-submissions   body: { siteId, reportPeriodId, submittedBy }
        [HttpPost]
        public async Task<IActionResult> MarkSubmitted([FromBody] SiteSubmissionCreateDto request)
        {
            var result = await _entry.MarkSubmittedAsync(request);
            return Ok(result);
        }
    }
}
