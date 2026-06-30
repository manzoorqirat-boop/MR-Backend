namespace SiteReportApp.Models
{
    // ---- Monthly Site Scorecard (the 20-sheet QC/QA metrics workbook) ----
    //
    // This is intentionally a single generic table rather than 20 separate entities.
    // The 20 metric sheets are highly heterogeneous (3–12 columns each, different
    // input/computed mixes) and new sheets get added over time, so modelling each
    // as its own EF entity + controller + DTO would be unmaintainable.
    //
    // Instead, every data cell for a given (Site, ReportPeriod, Metric, RowIndex)
    // is stored as a JSON object in CellsJson, keyed by the column "key" defined in
    // ScorecardSchema. Computed columns are NOT stored — they are derived on read
    // from the schema formulas, so the maths always matches the template.
    //
    // RowIndex supports the "multi-row" sheets (Human Error, Audit Performance,
    // Man Power status, …) where one site/month has several rows. Single-row
    // sheets simply use RowIndex = 0.
    public class ScorecardEntry
    {
        public int Id { get; set; }

        public int SiteId { get; set; }
        public Site Site { get; set; } = null!;

        public int ReportPeriodId { get; set; }
        public ReportPeriod ReportPeriod { get; set; } = null!;

        // Stable metric key from ScorecardSchema (e.g. "humanError", "oosRate").
        public string MetricKey { get; set; } = string.Empty;

        // 0-based row number within this site/period/metric.
        public int RowIndex { get; set; }

        // Input cell values as a JSON object: { "columnKey": value, ... }.
        // Stored as text/jsonb; values are strings/numbers as entered.
        public string CellsJson { get; set; } = "{}";
    }
}
