namespace SiteReportApp.Dtos
{
    // ---- Site submission status ----
    public class SiteSubmissionCreateDto
    {
        public int SiteId { get; set; }
        public int ReportPeriodId { get; set; }
        public string SubmittedBy { get; set; } = string.Empty;
    }

    // Corporate decision on a submitted month
    public class SubmissionReviewDto
    {
        public string Decision { get; set; } = string.Empty;   // "Approve" | "Return"
        public string? Comments { get; set; }
    }

    // One row of the corporate review grid: every active site, its workflow
    // state for the period, and how many scorecard sheets it has filled.
    public class SubmissionOverviewRowDto
    {
        public int SiteId { get; set; }
        public string SiteName { get; set; } = string.Empty;
        public string SiteCode { get; set; } = string.Empty;
        public int? SubmissionId { get; set; }
        public string Status { get; set; } = "NotStarted";
        public string? SubmittedBy { get; set; }
        public DateTime? SubmittedAtUtc { get; set; }
        public string? ReviewedBy { get; set; }
        public DateTime? ReviewedAtUtc { get; set; }
        public string? ReviewComments { get; set; }
        public int ScorecardMetricCount { get; set; }   // distinct scorecard sheets with data
    }
}
