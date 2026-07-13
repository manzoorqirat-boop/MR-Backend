namespace SiteReportApp.Models
{
    // A proposed change to an initiative whose month is frozen (submitted or
    // approved). Carries a snapshot of the original values and the proposed
    // ones as JSON, plus the site's justification and corporate's decision —
    // a complete audit trail for every post-submission correction.
    public class InitiativeChangeRequest
    {
        public int Id { get; set; }
        public int InitiativeId { get; set; }
        public Initiative Initiative { get; set; } = null!;

        public ChangeRequestType RequestType { get; set; } = ChangeRequestType.Update;
        public string OriginalJson { get; set; } = "{}";   // editable fields at request time
        public string ProposedJson { get; set; } = "{}";   // requested values (empty for Delete)
        public string Justification { get; set; } = string.Empty;

        public ChangeRequestStatus Status { get; set; } = ChangeRequestStatus.Pending;
        public string RequestedBy { get; set; } = string.Empty;
        public DateTime RequestedAtUtc { get; set; } = DateTime.UtcNow;
        public string? DecidedBy { get; set; }
        public DateTime? DecidedAtUtc { get; set; }
        public string? DecisionComments { get; set; }
    }
}
