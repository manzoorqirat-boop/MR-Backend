namespace SiteReportApp.Dtos
{
    // ---- Create/Update requests for Initiatives (sheets 2-6) ----
    public class InitiativeCreateDto
    {
        public int SiteId { get; set; }
        public int ReportPeriodId { get; set; }
        public string Type { get; set; } = string.Empty;   // matches InitiativeType enum name
        public int SerialNo { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;
        public string? Category { get; set; }
        public string FacilitatorName { get; set; } = string.Empty;
        public string DepartmentHead { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;  // matches CompletionStatus enum name
        public string? Remarks { get; set; }
    }

    public class InitiativeBulkCreateDto
    {
        public int SiteId { get; set; }
        public int ReportPeriodId { get; set; }
        public string Type { get; set; } = string.Empty;
        public List<InitiativeCreateDto> Rows { get; set; } = new();
    }

    // ---- Training (sheet 1) ----
    public class TrainingCreateDto
    {
        public int SiteId { get; set; }
        public int ReportPeriodId { get; set; }
        public int SerialNo { get; set; }
        public string Topic { get; set; } = string.Empty;
        public string TrainingImpartedBy { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;  // matches TrainingStatus enum name
    }

    public class TrainingBulkCreateDto
    {
        public int SiteId { get; set; }
        public int ReportPeriodId { get; set; }
        public List<TrainingCreateDto> Rows { get; set; } = new();
    }

    // ---- Cost Savings (sheet 7) ----
    public class CostSavingCreateDto
    {
        public int SiteId { get; set; }
        public int ReportPeriodId { get; set; }
        public int SerialNo { get; set; }
        public string ProjectName { get; set; } = string.Empty;
        public decimal PotentialSavingLacs { get; set; }
        public string ProjectStatus { get; set; } = string.Empty; // matches ProjectStatus enum name
        public bool ValidatedByFinance { get; set; }
        public string? Remarks { get; set; }
    }

    public class CostSavingBulkCreateDto
    {
        public int SiteId { get; set; }
        public int ReportPeriodId { get; set; }
        public List<CostSavingCreateDto> Rows { get; set; } = new();
    }

    // ---- Generic result for any bulk/import operation ----
    public class ImportResultDto
    {
        public int RowsAccepted { get; set; }
        public int RowsRejected { get; set; }
        public List<string> Errors { get; set; } = new();
    }

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
    // state for the period, and how much data it has actually entered.
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
        public int TrainingCount { get; set; }
        public int InitiativeCount { get; set; }
        public int CostSavingCount { get; set; }
        public int ScorecardMetricCount { get; set; }   // distinct scorecard sheets with data
    }
}
