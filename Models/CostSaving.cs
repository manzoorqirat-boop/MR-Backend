namespace SiteReportApp.Models
{
    // ---- Cost Savings (Analytics) ----
    // A brand-new tracking table (no equivalent scorecard sheet exists for this yet):
    // one row per cost-saving item logged against a site + reporting month. Kept
    // separate from ScorecardEntry since this isn't part of the 20-sheet template —
    // it's simple enough (one amount + a description) not to need the generic
    // cells-JSON approach.
    public class CostSaving
    {
        public int Id { get; set; }

        public int SiteId { get; set; }
        public Site Site { get; set; } = null!;

        public int ReportPeriodId { get; set; }
        public ReportPeriod ReportPeriod { get; set; } = null!;

        // Stored as decimal for currency precision (no rounding drift when summed).
        public decimal AmountSaved { get; set; }
        public string Description { get; set; } = string.Empty;

        public string CreatedBy { get; set; } = string.Empty;
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    }
}
