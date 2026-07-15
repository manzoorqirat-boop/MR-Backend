using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SiteReportApp.Data;
using SiteReportApp.Models;
using SiteReportApp.Services;

namespace SiteReportApp.Controllers
{
    public class ReminderSettingsDto
    {
        public bool Enabled { get; set; } = true;
        public int LeadDays { get; set; } = 15;
    }

    public class TestEmailDto
    {
        public string To { get; set; } = string.Empty;
    }

    // Corporate configuration for the QA-IT review reminders:
    // enabled + X days before the scheduled month, plus a test-email button.
    [ApiController]
    [Route("api/settings/qa-it-reminder")]
    [Authorize(Roles = "Corporate")]
    public class ReminderSettingsController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly EmailService _email;
        private readonly ReviewReminderService _reminder;

        public ReminderSettingsController(AppDbContext db, EmailService email, ReviewReminderService reminder)
        {
            _db = db;
            _email = email;
            _reminder = reminder;
        }

        private async Task<string> GetAsync(string key, string fallback)
        {
            var s = await _db.AppSettings.FirstOrDefaultAsync(a => a.Key == key);
            return s?.Value ?? fallback;
        }

        private async Task SetAsync(string key, string value)
        {
            var s = await _db.AppSettings.FirstOrDefaultAsync(a => a.Key == key);
            if (s == null) _db.AppSettings.Add(new AppSetting { Key = key, Value = value });
            else s.Value = value;
        }

        // GET /api/settings/qa-it-reminder
        [HttpGet]
        public async Task<IActionResult> Get()
        {
            var enabled = await GetAsync(SettingKeys.QaItReminderEnabled, "true");
            var leadDays = await GetAsync(SettingKeys.QaItReminderLeadDays, "15");
            return Ok(new
            {
                enabled = string.Equals(enabled, "true", StringComparison.OrdinalIgnoreCase),
                leadDays = int.TryParse(leadDays, out var x) ? x : 15,
                smtpConfigured = EmailService.IsConfigured
            });
        }

        // PUT /api/settings/qa-it-reminder  body: { enabled, leadDays }
        [HttpPut]
        public async Task<IActionResult> Update([FromBody] ReminderSettingsDto dto)
        {
            if (dto.LeadDays < 0 || dto.LeadDays > 365)
                return BadRequest(new { error = "Lead days must be between 0 and 365." });
            await SetAsync(SettingKeys.QaItReminderEnabled, dto.Enabled ? "true" : "false");
            await SetAsync(SettingKeys.QaItReminderLeadDays, dto.LeadDays.ToString());
            await _db.SaveChangesAsync();
            return Ok(new { ok = true });
        }

        // POST /api/settings/qa-it-reminder/test  body: { to } — verify SMTP setup
        [HttpPost("test")]
        public async Task<IActionResult> TestEmail([FromBody] TestEmailDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.To))
                return BadRequest(new { error = "Recipient address is required." });
            try
            {
                await _email.SendAsync(new[] { dto.To.Trim() },
                    "[QA-IT] Test email — QMS Site Reporting",
                    "SMTP is configured correctly. Periodic review reminders will be delivered like this message.");
                return Ok(new { ok = true });
            }
            catch (Exception ex)
            {
                return Conflict(new { error = ex.Message });
            }
        }

        // POST /api/settings/qa-it-reminder/run-now — trigger a scan immediately
        // (useful after changing X or adding register rows; dedup still applies).
        [HttpPost("run-now")]
        public async Task<IActionResult> RunNow()
        {
            try
            {
                await _reminder.RunOnceAsync(HttpContext.RequestAborted);
                return Ok(new { ok = true });
            }
            catch (Exception ex)
            {
                return Conflict(new { error = ex.Message });
            }
        }
    }
}
