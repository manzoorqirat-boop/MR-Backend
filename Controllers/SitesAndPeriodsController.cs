using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SiteReportApp.Auth;
using SiteReportApp.Data;
using SiteReportApp.Dtos;
using SiteReportApp.Models;
using SiteReportApp.Services;

namespace SiteReportApp.Controllers
{
    [ApiController]
    [Route("api/sites")]
    [Authorize]
    public class SitesController : ControllerBase
    {
        private readonly AppDbContext _db;

        public SitesController(AppDbContext db)
        {
            _db = db;
        }

        // GET /api/sites — site users see only their own site so every picker
        // in the UI collapses naturally to the one site they belong to.
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var query = _db.Sites.Where(s => s.IsActive);
            if (!User.IsCorporate())
            {
                var siteId = User.GetSiteId();
                query = query.Where(s => s.Id == siteId);
            }
            var sites = await query.OrderBy(s => s.Name).ToListAsync();
            return Ok(sites);
        }

        // POST /api/sites   body: { name, code } — corporate only
        [HttpPost]
        [Authorize(Roles = "Corporate")]
        public async Task<IActionResult> Create([FromBody] Site site)
        {
            _db.Sites.Add(site);
            await _db.SaveChangesAsync();
            return CreatedAtAction(nameof(GetAll), new { id = site.Id }, site);
        }
    }

    [ApiController]
    [Route("api/report-periods")]
    [Authorize]
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

        // POST /api/report-periods   body: { month, year } — corporate only
        // Idempotent-ish: returns existing period if month/year already exists
        [HttpPost]
        [Authorize(Roles = "Corporate")]
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

        // PATCH /api/report-periods/5/lock — corporate locks the month once all sites are approved
        [HttpPatch("{id}/lock")]
        [Authorize(Roles = "Corporate")]
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
    [Authorize]
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
        // Site users only ever see their own row; corporate sees all sites.
        [HttpGet]
        public async Task<IActionResult> GetForPeriod([FromQuery] int reportPeriodId)
        {
            var query = _db.SiteSubmissions.Where(ss => ss.ReportPeriodId == reportPeriodId);
            if (!User.IsCorporate())
            {
                var siteId = User.GetSiteId();
                query = query.Where(ss => ss.SiteId == siteId);
            }
            var data = await query.Include(ss => ss.Site).ToListAsync();
            return Ok(data);
        }

        // POST /api/site-submissions/submit   body: { siteId, reportPeriodId }
        // A site submits its month to corporate for review. Once submitted the
        // site's data for that period is read-only until corporate returns it.
        [HttpPost("submit")]
        public async Task<IActionResult> Submit([FromBody] SiteSubmissionCreateDto request)
        {
            if (!User.CanAccessSite(request.SiteId)) return Forbid();
            try
            {
                request.SubmittedBy = User.GetDisplayName();
                var result = await _entry.SubmitAsync(request);
                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { error = ex.Message });
            }
        }

        // POST /api/site-submissions/{id}/review   body: { decision: "Approve"|"Return", comments }
        // Corporate approves the site's month, or returns it with comments so
        // the site can revise and resubmit.
        [HttpPost("{id}/review")]
        [Authorize(Roles = "Corporate")]
        public async Task<IActionResult> Review(int id, [FromBody] SubmissionReviewDto dto)
        {
            try
            {
                var result = await _entry.ReviewAsync(id, dto, User.GetDisplayName());
                return Ok(result);
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { error = ex.Message });
            }
        }

        // GET /api/site-submissions/overview?reportPeriodId=5 — corporate review grid.
        // One row per active site with submission state + how much data the site
        // has actually entered, so reviewers can spot empty submissions at a glance.
        [HttpGet("overview")]
        [Authorize(Roles = "Corporate")]
        public async Task<IActionResult> Overview([FromQuery] int reportPeriodId)
        {
            var overview = await _entry.GetOverviewAsync(reportPeriodId);
            return Ok(overview);
        }
    }
}
