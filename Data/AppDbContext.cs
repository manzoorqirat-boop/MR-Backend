using Microsoft.EntityFrameworkCore;
using SiteReportApp.Models;

namespace SiteReportApp.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Site> Sites => Set<Site>();
        public DbSet<ReportPeriod> ReportPeriods => Set<ReportPeriod>();
        public DbSet<SiteSubmission> SiteSubmissions => Set<SiteSubmission>();
        public DbSet<Initiative> Initiatives => Set<Initiative>();
        public DbSet<TrainingRecord> TrainingRecords => Set<TrainingRecord>();
        public DbSet<CostSavingInitiative> CostSavingInitiatives => Set<CostSavingInitiative>();
        public DbSet<ScorecardEntry> ScorecardEntries => Set<ScorecardEntry>();
        public DbSet<User> Users => Set<User>();
        public DbSet<InitiativeAttachment> InitiativeAttachments => Set<InitiativeAttachment>();
        public DbSet<InitiativeChangeRequest> InitiativeChangeRequests => Set<InitiativeChangeRequest>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // ---- InitiativeChangeRequest ----
            modelBuilder.Entity<InitiativeChangeRequest>()
                .HasOne(cr => cr.Initiative)
                .WithMany()
                .HasForeignKey(cr => cr.InitiativeId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<InitiativeChangeRequest>()
                .Property(cr => cr.Status)
                .HasConversion<string>();

            modelBuilder.Entity<InitiativeChangeRequest>()
                .Property(cr => cr.RequestType)
                .HasConversion<string>();

            modelBuilder.Entity<InitiativeChangeRequest>()
                .HasIndex(cr => new { cr.InitiativeId, cr.Status });

            // ---- InitiativeAttachment ----
            modelBuilder.Entity<InitiativeAttachment>()
                .HasOne(a => a.Initiative)
                .WithMany()
                .HasForeignKey(a => a.InitiativeId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<InitiativeAttachment>()
                .HasIndex(a => a.InitiativeId);

            // ---- User ----
            modelBuilder.Entity<User>()
                .HasIndex(u => u.Username)
                .IsUnique();

            modelBuilder.Entity<User>()
                .Property(u => u.Role)
                .HasConversion<string>();

            modelBuilder.Entity<User>()
                .HasOne(u => u.Site)
                .WithMany()
                .HasForeignKey(u => u.SiteId)
                .OnDelete(DeleteBehavior.Restrict);
            // ---- Site ----
            modelBuilder.Entity<Site>()
                .HasIndex(s => s.Code)
                .IsUnique();

            // ---- ReportPeriod ----
            modelBuilder.Entity<ReportPeriod>()
                .HasIndex(rp => new { rp.Year, rp.Month })
                .IsUnique();

            // ---- SiteSubmission ----
            modelBuilder.Entity<SiteSubmission>()
                .HasIndex(ss => new { ss.SiteId, ss.ReportPeriodId })
                .IsUnique();

            modelBuilder.Entity<SiteSubmission>()
                .Property(ss => ss.Status)
                .HasConversion<string>();

            modelBuilder.Entity<SiteSubmission>()
                .HasOne(ss => ss.Site)
                .WithMany()
                .HasForeignKey(ss => ss.SiteId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<SiteSubmission>()
                .HasOne(ss => ss.ReportPeriod)
                .WithMany()
                .HasForeignKey(ss => ss.ReportPeriodId)
                .OnDelete(DeleteBehavior.Restrict);

            // ---- Initiative ----
            // Composite index: this is the shape every analytics query filters by
            modelBuilder.Entity<Initiative>()
                .HasIndex(i => new { i.ReportPeriodId, i.SiteId, i.Type });

            // Natural key: one row per serial within a site/period/type. Prevents
            // duplicate rows from a re-import or a double-submit (see DataEntryService upsert).
            modelBuilder.Entity<Initiative>()
                .HasIndex(i => new { i.SiteId, i.ReportPeriodId, i.Type, i.SerialNo })
                .IsUnique();

            modelBuilder.Entity<Initiative>()
                .HasOne(i => i.Site)
                .WithMany(s => s.Initiatives)
                .HasForeignKey(i => i.SiteId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Initiative>()
                .HasOne(i => i.ReportPeriod)
                .WithMany(rp => rp.Initiatives)
                .HasForeignKey(i => i.ReportPeriodId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Initiative>()
                .Property(i => i.Type)
                .HasConversion<string>();

            modelBuilder.Entity<Initiative>()
                .Property(i => i.Status)
                .HasConversion<string>();

            // ---- TrainingRecord ----
            modelBuilder.Entity<TrainingRecord>()
                .HasIndex(t => new { t.ReportPeriodId, t.SiteId });

            modelBuilder.Entity<TrainingRecord>()
                .HasIndex(t => new { t.SiteId, t.ReportPeriodId, t.SerialNo })
                .IsUnique();

            modelBuilder.Entity<TrainingRecord>()
                .HasOne(t => t.Site)
                .WithMany(s => s.TrainingRecords)
                .HasForeignKey(t => t.SiteId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<TrainingRecord>()
                .HasOne(t => t.ReportPeriod)
                .WithMany(rp => rp.TrainingRecords)
                .HasForeignKey(t => t.ReportPeriodId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<TrainingRecord>()
                .Property(t => t.Status)
                .HasConversion<string>();

            // ---- CostSavingInitiative ----
            modelBuilder.Entity<CostSavingInitiative>()
                .HasIndex(c => new { c.ReportPeriodId, c.SiteId });

            modelBuilder.Entity<CostSavingInitiative>()
                .HasIndex(c => new { c.SiteId, c.ReportPeriodId, c.SerialNo })
                .IsUnique();

            modelBuilder.Entity<CostSavingInitiative>()
                .HasOne(c => c.Site)
                .WithMany(s => s.CostSavings)
                .HasForeignKey(c => c.SiteId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<CostSavingInitiative>()
                .HasOne(c => c.ReportPeriod)
                .WithMany(rp => rp.CostSavings)
                .HasForeignKey(c => c.ReportPeriodId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<CostSavingInitiative>()
                .Property(c => c.ProjectStatus)
                .HasConversion<string>();

            // Decimal precision for money column (avoid Npgsql warning/truncation)
            modelBuilder.Entity<CostSavingInitiative>()
                .Property(c => c.PotentialSavingLacs)
                .HasPrecision(14, 2);

            // ---- ScorecardEntry (Monthly Site Scorecard, 20-sheet metrics) ----
            // One row per (site, period, metric, rowIndex). The composite index is the
            // shape every read/analytics query filters by. CellsJson holds the input cells.
            modelBuilder.Entity<ScorecardEntry>()
                .HasIndex(e => new { e.SiteId, e.ReportPeriodId, e.MetricKey });

            modelBuilder.Entity<ScorecardEntry>()
                .HasIndex(e => new { e.SiteId, e.ReportPeriodId, e.MetricKey, e.RowIndex })
                .IsUnique();

            // Analytics groups by metric + period across sites.
            modelBuilder.Entity<ScorecardEntry>()
                .HasIndex(e => new { e.MetricKey, e.ReportPeriodId });

            modelBuilder.Entity<ScorecardEntry>()
                .Property(e => e.MetricKey)
                .HasMaxLength(64);

            modelBuilder.Entity<ScorecardEntry>()
                .HasOne(e => e.Site)
                .WithMany()
                .HasForeignKey(e => e.SiteId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ScorecardEntry>()
                .HasOne(e => e.ReportPeriod)
                .WithMany()
                .HasForeignKey(e => e.ReportPeriodId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
