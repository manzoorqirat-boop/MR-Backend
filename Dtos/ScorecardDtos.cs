namespace SiteReportApp.Dtos
{
    // ---- Save: one metric's rows for a site/period ----
    // Cells holds only the *input* columns keyed by column key; computed columns
    // are ignored on save and recomputed on read.
    public class ScorecardRowDto
    {
        public int RowIndex { get; set; }
        public Dictionary<string, string?> Cells { get; set; } = new();
    }

    public class ScorecardSaveDto
    {
        public int SiteId { get; set; }
        public int ReportPeriodId { get; set; }
        public string MetricKey { get; set; } = string.Empty;
        public List<ScorecardRowDto> Rows { get; set; } = new();
    }

    // ---- Read: rows with computed columns resolved ----
    public class ScorecardResolvedRowDto
    {
        public int Id { get; set; }
        public int RowIndex { get; set; }
        // Every column key -> value as a string ("" / "-" for blank or div/0).
        public Dictionary<string, string> Cells { get; set; } = new();
    }

    // ---- Schema description sent to the frontend (so it never hardcodes columns) ----
    public class ScColumnDto
    {
        public string Key { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;   // Number | Text | Date | Computed
        public string? Formula { get; set; }
    }

    public class ScMetricDto
    {
        public string Key { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string SheetName { get; set; } = string.Empty;
        public bool MultiRow { get; set; }
        public int Order { get; set; }
        public List<ScColumnDto> Columns { get; set; } = new();
    }

    // ---- Analytics: a single value of one metric/column for one site & period ----
    public class ScorecardAnalyticsPointDto
    {
        public int SiteId { get; set; }
        public string SiteName { get; set; } = string.Empty;
        public int ReportPeriodId { get; set; }
        public string PeriodLabel { get; set; } = string.Empty; // "2026-05"
        public int Year { get; set; }
        public int Month { get; set; }
        public string MetricKey { get; set; } = string.Empty;
        public string ColumnKey { get; set; } = string.Empty;
        // Aggregated numeric value across that site/period/metric/column (sum of inputs,
        // or recomputed ratio — see AggregationMode).
        public double? Value { get; set; }
    }

    // ---- Analytics request shape ----
    public class ScorecardAnalyticsQueryDto
    {
        public string MetricKey { get; set; } = string.Empty;
        public string? ColumnKey { get; set; }     // null = all numeric/computed columns
        public int FromYear { get; set; }
        public int FromMonth { get; set; }
        public int ToYear { get; set; }
        public int ToMonth { get; set; }
        public List<int>? SiteIds { get; set; }    // null/empty = all sites
        public string Granularity { get; set; } = "monthly"; // monthly | quarterly
    }

    // ---- Excel import result ----
    public class ScorecardImportSheetResultDto
    {
        public string SheetName { get; set; } = string.Empty;
        public string? MetricKey { get; set; }
        public int RowsAccepted { get; set; }
        public int RowsRejected { get; set; }
        public bool Matched { get; set; }
    }

    public class ScorecardImportResultDto
    {
        public List<ScorecardImportSheetResultDto> Sheets { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
    }
}
