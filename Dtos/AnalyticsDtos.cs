namespace SiteReportApp.Dtos
{
    // ==========================================================================
    //  Initiatives — derived from the "Equipment Qualification" scorecard metric
    //  (Type of Activities / Product Segment rows with Initiated / Carry Forward /
    //  Completed / In-Progress counts). Those columns are stored as free text in
    //  the template, so values are parsed as numbers where possible; non-numeric
    //  cells are ignored rather than breaking the summary.
    // ==========================================================================
    public class InitiativeTypeSummaryDto
    {
        public string TypeOfActivities { get; set; } = string.Empty;
        public string ProductSegment { get; set; } = string.Empty;
        public double Initiated { get; set; }
        public double CarryForward { get; set; }
        public double Completed { get; set; }
        public double InProgress { get; set; }
    }

    public class InitiativeSiteSummaryDto
    {
        public int SiteId { get; set; }
        public string SiteName { get; set; } = string.Empty;
        public double Initiated { get; set; }
        public double CarryForward { get; set; }
        public double Completed { get; set; }
        public double InProgress { get; set; }
    }

    public class InitiativeSummaryDto
    {
        public int ReportPeriodId { get; set; }
        public string PeriodLabel { get; set; } = string.Empty;
        public double TotalInitiated { get; set; }
        public double TotalCarryForward { get; set; }
        public double TotalCompleted { get; set; }
        public double TotalInProgress { get; set; }
        public List<InitiativeTypeSummaryDto> ByType { get; set; } = new();
        public List<InitiativeSiteSummaryDto> BySite { get; set; } = new();
    }

    // ==========================================================================
    //  Training — derived from the "Training %" scorecard metric (single row
    //  per site/period).
    // ==========================================================================
    public class TrainingSiteSummaryDto
    {
        public int SiteId { get; set; }
        public string SiteName { get; set; } = string.Empty;
        public double? SopCompletionPct { get; set; }
        public double? GmpCompletionPct { get; set; }
        public double? FunctionalCompletionPct { get; set; }
        public double ExternalTrainingCount { get; set; }
    }

    public class TrainingSummaryDto
    {
        public int ReportPeriodId { get; set; }
        public string PeriodLabel { get; set; } = string.Empty;
        public double? AvgSopCompletionPct { get; set; }
        public double? AvgGmpCompletionPct { get; set; }
        public double? AvgFunctionalCompletionPct { get; set; }
        public double TotalExternalTrainings { get; set; }
        public List<TrainingSiteSummaryDto> Sites { get; set; } = new();
    }

    // ==========================================================================
    //  Cost Savings — backed by the new CostSavings table.
    // ==========================================================================
    public class CostSavingCreateDto
    {
        public int SiteId { get; set; }
        public int ReportPeriodId { get; set; }
        public decimal AmountSaved { get; set; }
        public string Description { get; set; } = string.Empty;
    }

    public class CostSavingSiteSummaryDto
    {
        public int SiteId { get; set; }
        public string SiteName { get; set; } = string.Empty;
        public decimal TotalSaved { get; set; }
        public int ItemCount { get; set; }
    }

    public class CostSavingSummaryDto
    {
        public int ReportPeriodId { get; set; }
        public string PeriodLabel { get; set; } = string.Empty;
        public decimal TotalSaved { get; set; }
        public List<CostSavingSiteSummaryDto> BySite { get; set; } = new();
    }

    public class CostSavingTrendPointDto
    {
        public int ReportPeriodId { get; set; }
        public string PeriodLabel { get; set; } = string.Empty;
        public int Year { get; set; }
        public int Month { get; set; }
        public decimal TotalSaved { get; set; }
    }

    // ==========================================================================
    //  Range analytics — one KPI per (metric, column), aggregated generically
    //  across every metric in ScorecardSchema for a date range. Mirrors
    //  ScorecardService.GetAnalyticsAsync's aggregation rule (sum inputs, then
    //  recompute ratios) but across all 20 sheets at once, optionally narrowed
    //  to one site, and optionally rolled up to quarters.
    // ==========================================================================
    public class RangeMetricPointDto
    {
        public string MetricKey { get; set; } = string.Empty;
        public string MetricTitle { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string ColumnKey { get; set; } = string.Empty;
        public string ColumnLabel { get; set; } = string.Empty;
        public string PeriodLabel { get; set; } = string.Empty; // "2026-05" or "2026-Q2"
        public int Year { get; set; }
        public int Month { get; set; }        // 0 for quarterly points
        public int Quarter { get; set; }       // 0 for monthly points
        public double? Value { get; set; }
    }

    public class RangeAnalyticsDto
    {
        public string Granularity { get; set; } = "monthly";
        public int? SiteId { get; set; }
        public List<RangeMetricPointDto> Points { get; set; } = new();
    }

    // ==========================================================================
    //  Monthly Global Report — single-period, all-metrics-at-once rollup for
    //  the head-office report screen/export.
    // ==========================================================================
    public class GlobalReportColumnValueDto
    {
        public string ColumnKey { get; set; } = string.Empty;
        public string ColumnLabel { get; set; } = string.Empty;
        public double? Value { get; set; }
    }

    public class GlobalReportSiteRowDto
    {
        public int SiteId { get; set; }
        public string SiteName { get; set; } = string.Empty;
        public List<GlobalReportColumnValueDto> Columns { get; set; } = new();
    }

    public class GlobalReportMetricDto
    {
        public string MetricKey { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public List<GlobalReportColumnValueDto> Overall { get; set; } = new();   // summed across all sites
        public List<GlobalReportSiteRowDto> BySite { get; set; } = new();
    }

    public class GlobalReportCategoryDto
    {
        public string Category { get; set; } = string.Empty;
        public List<GlobalReportMetricDto> Metrics { get; set; } = new();
    }

    public class GlobalReportDto
    {
        public int ReportPeriodId { get; set; }
        public string PeriodLabel { get; set; } = string.Empty;
        public InitiativeSummaryDto Initiatives { get; set; } = new();
        public TrainingSummaryDto Training { get; set; } = new();
        public CostSavingSummaryDto CostSavings { get; set; } = new();
        public List<GlobalReportCategoryDto> Categories { get; set; } = new();
    }
}
