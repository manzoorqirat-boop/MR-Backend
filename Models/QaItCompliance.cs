namespace SiteReportApp.Models
{
    // ============ QA-IT Compliance Activities ============
    // Periodic review register for computerized systems (equipment/instruments
    // with software), kept per site per YEAR — mirrors the paper form:
    // LOCATION / VERSION / YEAR header + one row per system.
    //
    // Dates are stored as ISO strings ("yyyy-MM-dd" for full dates,
    // "yyyy-MM" for month/year cells) — this register is document-like and
    // string storage avoids timezone pitfalls entirely.

    // Header of one site-year register (the VERSION box on the form).
    public class QaItRegister
    {
        public int Id { get; set; }
        public int SiteId { get; set; }
        public Site Site { get; set; } = null!;
        public int Year { get; set; }
        public string Version { get; set; } = string.Empty;
        public string UpdatedBy { get; set; } = string.Empty;
        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    }

    // One row of the register = one computerized system under periodic review.
    public class QaItPeriodicReview
    {
        public int Id { get; set; }
        public int SiteId { get; set; }
        public Site Site { get; set; } = null!;
        public int Year { get; set; }

        public int SerialNo { get; set; }
        // Chosen from the Equipment master; stored as a point-in-time snapshot so
        // historical registers stay intact even if the master changes later.
        public string EquipmentName { get; set; } = string.Empty;        // Name of the Equipment/Instrument
        public string EquipmentCode { get; set; } = string.Empty;        // Equipment/Instrument ID
        public string SoftwareNameVersion { get; set; } = string.Empty;  // Software Name & Version
        public string DepartmentArea { get; set; } = string.Empty;       // Department/Area
        public string SystemCategory { get; set; } = string.Empty;       // System Category
        public string InitialQualificationDate { get; set; } = "";       // yyyy-MM-dd
        public string LastPeriodicReviewDate { get; set; } = "";         // yyyy-MM-dd
        public string NextPlannedDue { get; set; } = "";                 // yyyy-MM (MMM/YYYY on the form)
        // Mandatory when NextPlannedDue deviates from the frequency-computed
        // date (base date + category frequency). Empty when auto-accepted.
        public string DueJustification { get; set; } = "";
        public string ActualDoneOn { get; set; } = "";                   // yyyy-MM; deadline = due month + 2
        public string ActualDoneBy { get; set; } = "";                   // person who performed the review
    }
}
