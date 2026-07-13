using System.Collections.Generic;

namespace SiteReportApp.Models
{
    public class Site
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;     // short code e.g. "SITE-A"
        public bool IsActive { get; set; } = true;

    }

    // Represents one reporting month, shared across all sites.
    // e.g. Month=6, Year=2026 represents "June 2026"
    public class ReportPeriod
    {
        public int Id { get; set; }
        public int Month { get; set; }     // 1-12
        public int Year { get; set; }
        public ReportPeriodStatus Status { get; set; } = ReportPeriodStatus.Open;


        public string DisplayName => $"{Year}-{Month:D2}";
    }

    // Per-site submission tracking — one row per site+month. Carries the whole
    // review workflow: the site submits, corporate approves or returns with comments.
    public class SiteSubmission
    {
        public int Id { get; set; }
        public int SiteId { get; set; }
        public Site Site { get; set; } = null!;
        public int ReportPeriodId { get; set; }
        public ReportPeriod ReportPeriod { get; set; } = null!;
        public bool IsSubmitted { get; set; }                    // kept for backward compat; true while Status is Submitted/Approved
        public SubmissionStatus Status { get; set; } = SubmissionStatus.NotStarted;
        public DateTime? SubmittedAtUtc { get; set; }
        public string? SubmittedBy { get; set; }
        public DateTime? ReviewedAtUtc { get; set; }
        public string? ReviewedBy { get; set; }
        public string? ReviewComments { get; set; }              // corporate feedback when returning for revision
    }
}
