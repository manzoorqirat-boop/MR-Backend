namespace SiteReportApp.Models
{
    // ============ Master Data ============
    // Controlled lists consumed as dropdowns by the QA-IT compliance register
    // (and future modules). Managed by corporate on the Admin page.

    // Equipment/Instrument master — per site (an HPLC at one location is not
    // at another). Name and ID are the two master fields on the paper form.
    public class Equipment
    {
        public int Id { get; set; }
        public int SiteId { get; set; }
        public Site Site { get; set; } = null!;
        public string Name { get; set; } = string.Empty;   // Name of the Equipment/Instrument
        public string Code { get; set; } = string.Empty;   // Equipment/Instrument ID
        public bool IsActive { get; set; } = true;
    }

    // Generic controlled list: one table serves Department/Area, System
    // Category, and any master list added later — keyed by ListKey.
    public static class MasterListKeys
    {
        public const string Department = "department";
        public const string SystemCategory = "systemCategory";
        public static readonly string[] All = { Department, SystemCategory };
    }

    public class MasterListItem
    {
        public int Id { get; set; }
        public string ListKey { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public int SortOrder { get; set; }
        // Periodic review frequency in years — meaningful for the System
        // Category list (Critical/Cat5=1, Major/Cat4=2, Minor/Cat3=3).
        public int? FrequencyYears { get; set; }
    }
}
