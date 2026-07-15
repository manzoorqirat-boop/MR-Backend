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
        public DbSet<ScorecardEntry> ScorecardEntries => Set<ScorecardEntry>();
        public DbSet<User> Users => Set<User>();
        public DbSet<Equipment> Equipments => Set<Equipment>();
        public DbSet<AppSetting> AppSettings => Set<AppSetting>();
        public DbSet<NotificationLog> NotificationLogs => Set<NotificationLog>();
        public DbSet<MasterListItem> MasterListItems => Set<MasterListItem>();
        public DbSet<QaItRegister> QaItRegisters => Set<QaItRegister>();
        public DbSet<QaItPeriodicReview> QaItPeriodicReviews => Set<QaItPeriodicReview>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // ---- Settings & notifications ----
            modelBuilder.Entity<AppSetting>()
                .HasIndex(a => a.Key)
                .IsUnique();

            modelBuilder.Entity<NotificationLog>()
                .HasIndex(n => n.DedupKey);

            // ---- Master data ----
            modelBuilder.Entity<Equipment>()
                .HasIndex(e => new { e.SiteId, e.Code })
                .IsUnique();

            modelBuilder.Entity<Equipment>()
                .HasOne(e => e.Site)
                .WithMany()
                .HasForeignKey(e => e.SiteId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<MasterListItem>()
                .HasIndex(i => new { i.ListKey, i.Value })
                .IsUnique();

            // ---- QA-IT Compliance ----
            modelBuilder.Entity<QaItRegister>()
                .HasIndex(r => new { r.SiteId, r.Year })
                .IsUnique();

            modelBuilder.Entity<QaItRegister>()
                .HasOne(r => r.Site)
                .WithMany()
                .HasForeignKey(r => r.SiteId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<QaItPeriodicReview>()
                .HasIndex(r => new { r.SiteId, r.Year });

            modelBuilder.Entity<QaItPeriodicReview>()
                .HasOne(r => r.Site)
                .WithMany()
                .HasForeignKey(r => r.SiteId)
                .OnDelete(DeleteBehavior.Cascade);

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
