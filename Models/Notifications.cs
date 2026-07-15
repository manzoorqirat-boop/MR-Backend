namespace SiteReportApp.Models
{
    // Simple key-value app settings (e.g. QA-IT reminder lead days) editable
    // by corporate from the Admin page.
    public class AppSetting
    {
        public int Id { get; set; }
        public string Key { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
    }

    public static class SettingKeys
    {
        public const string QaItReminderEnabled = "qaItReminderEnabled";   // "true"/"false"
        public const string QaItReminderLeadDays = "qaItReminderLeadDays"; // integer, days before scheduled date
    }

    // One row per notification actually sent — the dedup ledger that stops the
    // scheduler re-mailing the same reminder every run. DedupKey is content
    // based (site/year/equipment/dueMonth/type) so re-saving the register
    // (which recreates row ids) does not cause duplicate emails.
    public class NotificationLog
    {
        public int Id { get; set; }
        public string DedupKey { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;       // "upcoming" | "overdue"
        public string Recipients { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public DateTime SentAtUtc { get; set; } = DateTime.UtcNow;
    }
}
