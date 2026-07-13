namespace SiteReportApp.Models
{
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
