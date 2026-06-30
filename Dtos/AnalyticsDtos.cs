namespace SiteReportApp.Dtos
{
    // ---- Status breakdown, used for Initiatives (sheets 2-6) and Training (sheet 1) ----
    public class StatusBreakdownDto
    {
        public string Status { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    // One row per Site, for a given Initiative Type, for a given month
    public class SiteInitiativeSummaryDto
    {
        public int SiteId { get; set; }
        public string SiteName { get; set; } = string.Empty;
        public string InitiativeType { get; set; } = string.Empty;
        public int TotalCount { get; set; }
        public int CompletedCount { get; set; }
        public int InProgressCount { get; set; }
        public int DelayedCount { get; set; }
        public int NotStartedCount { get; set; }
        public double CompletionRatePercent { get; set; }
    }

    // Global rollup across all sites, for a given month, one row per InitiativeType
    public class GlobalInitiativeSummaryDto
    {
        public string InitiativeType { get; set; } = string.Empty;
        public int TotalCount { get; set; }
        public int CompletedCount { get; set; }
        public double CompletionRatePercent { get; set; }
        public List<SiteInitiativeSummaryDto> BySite { get; set; } = new();
    }

    public class TrainingSummaryDto
    {
        public int SiteId { get; set; }
        public string SiteName { get; set; } = string.Empty;
        public int TotalTrainings { get; set; }
        public int CompletedTrainings { get; set; }
        public int DepartmentsCovered { get; set; }
    }

    public class CostSavingSummaryDto
    {
        public int SiteId { get; set; }
        public string SiteName { get; set; } = string.Empty;
        public decimal TotalPotentialSavingLacs { get; set; }
        public decimal ValidatedSavingLacs { get; set; }     // only Finance-validated
        public int ProjectCount { get; set; }
        public int CompletedProjectCount { get; set; }
    }

    // Month-over-month trend point, reusable for any metric
    public class TrendPointDto
    {
        public string PeriodLabel { get; set; } = string.Empty; // e.g. "2026-05"
        public double Value { get; set; }
    }

    // Top-level payload for the monthly global report screen
    public class MonthlyGlobalReportDto
    {
        public string PeriodLabel { get; set; } = string.Empty;
        public List<GlobalInitiativeSummaryDto> Initiatives { get; set; } = new();
        public List<TrainingSummaryDto> Training { get; set; } = new();
        public List<CostSavingSummaryDto> CostSavings { get; set; } = new();
        public List<string> SitesNotSubmitted { get; set; } = new();
    }

    // =====================================================================
    // Range analytics: aggregates across a span of report periods, with a
    // monthly or quarterly granularity, optionally filtered to one site.
    // Powers the Analytics page (KPI cards + charts).
    // =====================================================================

    public class RangeKpiDto
    {
        public int PeriodsCount { get; set; }
        public int InitiativesTotal { get; set; }
        public int InitiativesCompleted { get; set; }
        public double InitiativeCompletionRate { get; set; }
        public int TrainingsTotal { get; set; }
        public int TrainingsCompleted { get; set; }
        public double TrainingCompletionRate { get; set; }
        public decimal CostSavingsPotentialLacs { get; set; }
        public decimal CostSavingsValidatedLacs { get; set; }
    }

    // One point on the time axis: a single month (e.g. "2025-03") or quarter ("2025-Q1").
    public class TimeBucketDto
    {
        public string Label { get; set; } = string.Empty;
        public int SortKey { get; set; }
        public int InitiativesTotal { get; set; }
        public int InitiativesCompleted { get; set; }
        public double InitiativeCompletionRate { get; set; }
        public int TrainingsTotal { get; set; }
        public int TrainingsCompleted { get; set; }
        public decimal CostSavingsPotentialLacs { get; set; }
        public decimal CostSavingsValidatedLacs { get; set; }
    }

    public class InitiativeTypeAggregateDto
    {
        public string Type { get; set; } = string.Empty;
        public int Total { get; set; }
        public int Completed { get; set; }
        public double CompletionRate { get; set; }
    }

    public class SiteAggregateDto
    {
        public int SiteId { get; set; }
        public string SiteName { get; set; } = string.Empty;
        public int InitiativesTotal { get; set; }
        public int InitiativesCompleted { get; set; }
        public int InitiativesInProgress { get; set; }
        public int InitiativesDelayed { get; set; }
        public int InitiativesNotStarted { get; set; }
        public double InitiativeCompletionRate { get; set; }
        public int TrainingsTotal { get; set; }
        public decimal CostSavingsPotentialLacs { get; set; }
        public decimal CostSavingsValidatedLacs { get; set; }
    }

    public class AnalyticsRangeDto
    {
        public string FromLabel { get; set; } = string.Empty;
        public string ToLabel { get; set; } = string.Empty;
        public string Granularity { get; set; } = "monthly";
        public int? SiteId { get; set; }
        public RangeKpiDto Kpis { get; set; } = new();
        public List<TimeBucketDto> Buckets { get; set; } = new();
        public List<InitiativeTypeAggregateDto> InitiativesByType { get; set; } = new();
        public List<StatusBreakdownDto> InitiativeStatusBreakdown { get; set; } = new();
        public List<SiteAggregateDto> BySite { get; set; } = new();
    }
}
