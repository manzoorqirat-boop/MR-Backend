using Microsoft.EntityFrameworkCore;
using SiteReportApp.Data;
using SiteReportApp.Models;

namespace SiteReportApp.Services
{
    // Background job for QA-IT periodic review reminders.
    // Every 6 hours it scans the register and emails each site's users:
    //   - "upcoming": today >= (1st of the planned due month) - X days
    //   - "overdue":  today >  (last day of due month + 2 months) and not done
    // X (lead days) and the on/off switch live in AppSettings (Admin page).
    // The NotificationLog dedup ledger guarantees each row+type mails once.
    public class ReviewReminderService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<ReviewReminderService> _logger;
        private static readonly TimeSpan Interval = TimeSpan.FromHours(6);

        public ReviewReminderService(IServiceScopeFactory scopeFactory, ILogger<ReviewReminderService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Small delay so startup/schema bootstrap finishes first.
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await RunOnceAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Review reminder run failed.");
                }
                await Task.Delay(Interval, stoppingToken);
            }
        }

        public async Task RunOnceAsync(CancellationToken ct)
        {
            if (!EmailService.IsConfigured)
            {
                _logger.LogInformation("Reminders skipped: SMTP not configured.");
                return;
            }

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var email = scope.ServiceProvider.GetRequiredService<EmailService>();

            var enabled = await GetSettingAsync(db, SettingKeys.QaItReminderEnabled, "true");
            if (!string.Equals(enabled, "true", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation("Reminders skipped: disabled in settings.");
                return;
            }
            var leadDaysRaw = await GetSettingAsync(db, SettingKeys.QaItReminderLeadDays, "15");
            var leadDays = int.TryParse(leadDaysRaw, out var x) ? Math.Clamp(x, 0, 365) : 15;

            var today = DateTime.UtcNow.Date;

            // Not-yet-done reviews with a planned month.
            var rows = await db.QaItPeriodicReviews
                .Include(r => r.Site)
                .Where(r => r.NextPlannedDue != "" && r.ActualDoneOn == "")
                .ToListAsync(ct);

            // Classify each row.
            var upcoming = new List<QaItPeriodicReview>();
            var overdue = new List<QaItPeriodicReview>();
            foreach (var r in rows)
            {
                if (!TryParseMonth(r.NextPlannedDue, out var dueStart)) continue;
                var reminderFrom = dueStart.AddDays(-leadDays);
                var deadline = dueStart.AddMonths(3).AddDays(-1); // end of due month + 2
                if (today > deadline) overdue.Add(r);
                else if (today >= reminderFrom) upcoming.Add(r);
            }

            await SendGroupedAsync(db, email, upcoming, "upcoming", leadDays, ct);
            await SendGroupedAsync(db, email, overdue, "overdue", leadDays, ct);
        }

        private async Task SendGroupedAsync(
            AppDbContext db, EmailService email, List<QaItPeriodicReview> rows,
            string type, int leadDays, CancellationToken ct)
        {
            if (rows.Count == 0) return;

            foreach (var siteGroup in rows.GroupBy(r => r.SiteId))
            {
                // Only rows not already notified for this type+due month.
                var fresh = new List<QaItPeriodicReview>();
                foreach (var r in siteGroup)
                {
                    var key = DedupKey(r, type);
                    if (!await db.NotificationLogs.AnyAsync(n => n.DedupKey == key, ct))
                        fresh.Add(r);
                }
                if (fresh.Count == 0) continue;

                var site = fresh[0].Site;
                var recipients = await db.Users
                    .Where(u => u.IsActive && u.SiteId == site.Id && u.Email != null && u.Email != "")
                    .Select(u => u.Email!)
                    .ToListAsync(ct);
                if (recipients.Count == 0)
                {
                    _logger.LogWarning("No email recipients for site {Site}; {Count} {Type} reminder(s) not sent.",
                        site.Name, fresh.Count, type);
                    continue;
                }

                var subject = type == "upcoming"
                    ? $"[QA-IT] {fresh.Count} periodic review(s) due soon — {site.Name}"
                    : $"[QA-IT] {fresh.Count} periodic review(s) OVERDUE — {site.Name}";

                var lines = fresh.Select(r =>
                    $"  - {r.EquipmentName} ({r.EquipmentCode}) | {r.DepartmentArea} | planned: {FmtMonth(r.NextPlannedDue)} | window closes: {FmtDeadline(r.NextPlannedDue)}");

                var intro = type == "upcoming"
                    ? $"The following computerized systems at {site.Name} reach their planned periodic review soon (reminder is set to {leadDays} day(s) before the scheduled month):"
                    : $"The following computerized systems at {site.Name} are PAST their review window (planned month +2 months) and have no actual review recorded:";

                var body =
                    intro + "\n\n" +
                    string.Join("\n", lines) + "\n\n" +
                    "Record the actual review date in the QA-IT Compliance register once completed.\n" +
                    "— QMS Site Reporting (automated reminder)";

                try
                {
                    await email.SendAsync(recipients, subject, body);
                    foreach (var r in fresh)
                    {
                        db.NotificationLogs.Add(new NotificationLog
                        {
                            DedupKey = DedupKey(r, type),
                            Type = type,
                            Recipients = string.Join(";", recipients),
                            Subject = subject
                        });
                    }
                    await db.SaveChangesAsync(ct);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed sending {Type} reminders for site {Site}.", type, site.Name);
                }
            }
        }

        private static string DedupKey(QaItPeriodicReview r, string type) =>
            $"{r.SiteId}|{r.Year}|{r.EquipmentCode}|{r.NextPlannedDue}|{type}";

        private static bool TryParseMonth(string ym, out DateTime firstOfMonth)
        {
            firstOfMonth = default;
            var parts = ym.Split('-');
            if (parts.Length != 2) return false;
            if (!int.TryParse(parts[0], out var y) || !int.TryParse(parts[1], out var m)) return false;
            if (m is < 1 or > 12) return false;
            firstOfMonth = new DateTime(y, m, 1, 0, 0, 0, DateTimeKind.Utc);
            return true;
        }

        private static readonly string[] Mon = { "JAN", "FEB", "MAR", "APR", "MAY", "JUN", "JUL", "AUG", "SEP", "OCT", "NOV", "DEC" };
        private static string FmtMonth(string ym) =>
            TryParseMonth(ym, out var d) ? $"{Mon[d.Month - 1]}/{d.Year}" : ym;
        private static string FmtDeadline(string ym) =>
            TryParseMonth(ym, out var d) ? FmtMonth(d.AddMonths(2).ToString("yyyy-MM")) : "";

        private static async Task<string> GetSettingAsync(AppDbContext db, string key, string fallback)
        {
            var s = await db.AppSettings.AsNoTracking().FirstOrDefaultAsync(a => a.Key == key);
            return s?.Value ?? fallback;
        }
    }
}
