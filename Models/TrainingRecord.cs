namespace SiteReportApp.Models
{
    // Sheet 1: People Competency - Training
    public class TrainingRecord
    {
        public int Id { get; set; }

        public int SiteId { get; set; }
        public Site Site { get; set; } = null!;

        public int ReportPeriodId { get; set; }
        public ReportPeriod ReportPeriod { get; set; } = null!;

        public int SerialNo { get; set; }
        public string Topic { get; set; } = string.Empty;
        public string TrainingImpartedBy { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;
        public TrainingStatus Status { get; set; }

        // Note: original sheet has a free-text "Months" column. Since each record
        // is already tied to a ReportPeriod (one row per month), we drop the
        // free-text Months field. If a single training spans multiple months,
        // create one TrainingRecord per ReportPeriod it covers instead.
    }
}
