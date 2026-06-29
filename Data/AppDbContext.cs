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

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
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
        }
    }
}
