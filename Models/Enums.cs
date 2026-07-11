namespace SiteReportApp.Models
{
    public enum InitiativeType
    {
        DocumentationSimplification = 1,
        RegulatoryCompliance = 2,
        ProductivityEnhancement = 3,
        LeanLaboratory = 4,
        Digitalization = 5
    }

    public enum CompletionStatus
    {
        NotStarted = 0,
        InProgress = 1,
        Completed = 2,
        Delayed = 3
    }

    public enum TrainingStatus
    {
        Planned = 0,
        Completed = 1,
        Postponed = 2
    }

    public enum ProjectStatus
    {
        Proposed = 0,
        InProgress = 1,
        Completed = 2,
        OnHold = 3
    }

    public enum ReportPeriodStatus
    {
        Open = 0,
        Submitted = 1,
        Locked = 2
    }

    // Who is logged in: a single-site user or the corporate/head-office team.
    public enum UserRole
    {
        SiteUser = 0,
        Corporate = 1
    }

    // Lifecycle of one site's monthly submission to corporate.
    //  NotStarted -> (site enters data) -> Submitted -> Approved
    //                                          \-> Returned (with comments) -> Submitted ...
    public enum SubmissionStatus
    {
        NotStarted = 0,
        Submitted = 1,
        Approved = 2,
        Returned = 3
    }
}
