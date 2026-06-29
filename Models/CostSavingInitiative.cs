namespace SiteReportApp.Models
{
    // Sheet 7: Cost Savings
    public class CostSavingInitiative
    {
        public int Id { get; set; }

        public int SiteId { get; set; }
        public Site Site { get; set; } = null!;

        public int ReportPeriodId { get; set; }
        public ReportPeriod ReportPeriod { get; set; } = null!;

        public int SerialNo { get; set; }
        public string ProjectName { get; set; } = string.Empty;
        public decimal PotentialSavingLacs { get; set; }
        public ProjectStatus ProjectStatus { get; set; }
        public bool ValidatedByFinance { get; set; }
        public string? Remarks { get; set; }
    }
}
