using System.Collections.Generic;

namespace SiteReportApp.Models
{
    public class Site
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;     // short code e.g. "SITE-A"
        public bool IsActive { get; set; } = true;

        public ICollection<Initiative> Initiatives { get; set; } = new List<Initiative>();
        public ICollection<TrainingRecord> TrainingRecords { get; set; } = new List<TrainingRecord>();
        public ICollection<CostSavingInitiative> CostSavings { get; set; } = new List<CostSavingInitiative>();
    }

    // Represents one reporting month, shared across all sites.
    // e.g. Month=6, Year=2026 represents "June 2026"
    public class ReportPeriod
    {
        public int Id { get; set; }
        public int Month { get; set; }     // 1-12
        public int Year { get; set; }
        public ReportPeriodStatus Status { get; set; } = ReportPeriodStatus.Open;

        public ICollection<Initiative> Initiatives { get; set; } = new List<Initiative>();
        public ICollection<TrainingRecord> TrainingRecords { get; set; } = new List<TrainingRecord>();
        public ICollection<CostSavingInitiative> CostSavings { get; set; } = new List<CostSavingInitiative>();

        public string DisplayName => $"{Year}-{Month:D2}";
    }

    // Per-site submission tracking — was this site's data for this month submitted, and by whom
    public class SiteSubmission
    {
        public int Id { get; set; }
        public int SiteId { get; set; }
        public Site Site { get; set; } = null!;
        public int ReportPeriodId { get; set; }
        public ReportPeriod ReportPeriod { get; set; } = null!;
        public bool IsSubmitted { get; set; }
        public DateTime? SubmittedAtUtc { get; set; }
        public string? SubmittedBy { get; set; }
    }
}
