namespace SiteReportApp.Models
{
    // Covers sheets: Documentation Simplification, Regulatory Compliance,
    // Productivity Enhancement, Lean Laboratory, Digitalization.
    // The "Type" field distinguishes which sheet a row belongs to.
    public class Initiative
    {
        public int Id { get; set; }

        public int SiteId { get; set; }
        public Site Site { get; set; } = null!;

        public int ReportPeriodId { get; set; }
        public ReportPeriod ReportPeriod { get; set; } = null!;

        public InitiativeType Type { get; set; }

        public int SerialNo { get; set; }                 // S.No from sheet
        public string Name { get; set; } = string.Empty;   // value of the sheet's "topic" column
        public string Department { get; set; } = string.Empty;
        public string? Category { get; set; }              // only Lean Lab & Digitalization use this
        public string FacilitatorName { get; set; } = string.Empty;
        public string DepartmentHead { get; set; } = string.Empty;
        public CompletionStatus Status { get; set; }
        public string? Remarks { get; set; }
    }
}
