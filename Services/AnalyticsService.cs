using Microsoft.EntityFrameworkCore;
using SiteReportApp.Data;
using SiteReportApp.Dtos;
using SiteReportApp.Models;

namespace SiteReportApp.Services
{
    public class AnalyticsService
    {
        private readonly AppDbContext _db;

        public AnalyticsService(AppDbContext db)
        {
            _db = db;
        }

        // ---- Initiatives (sheets 2-6): per-site, per-type breakdown for a given period ----
        public async Task<List<GlobalInitiativeSummaryDto>> GetInitiativeSummaryAsync(int reportPeriodId)
        {
            var raw = await _db.Initiatives
                .Where(i => i.ReportPeriodId == reportPeriodId)
                .Include(i => i.Site)
                .ToListAsync();

            var result = raw
                .GroupBy(i => i.Type)
                .Select(typeGroup => new GlobalInitiativeSummaryDto
                {
                    InitiativeType = typeGroup.Key.ToString(),
                    TotalCount = typeGroup.Count(),
                    CompletedCount = typeGroup.Count(i => i.Status == CompletionStatus.Completed),
                    CompletionRatePercent = typeGroup.Any()
                        ? Math.Round(100.0 * typeGroup.Count(i => i.Status == CompletionStatus.Completed) / typeGroup.Count(), 1)
                        : 0,
                    BySite = typeGroup
                        .GroupBy(i => new { i.SiteId, i.Site.Name })
                        .Select(siteGroup => new SiteInitiativeSummaryDto
                        {
                            SiteId = siteGroup.Key.SiteId,
                            SiteName = siteGroup.Key.Name,
                            InitiativeType = typeGroup.Key.ToString(),
                            TotalCount = siteGroup.Count(),
                            CompletedCount = siteGroup.Count(i => i.Status == CompletionStatus.Completed),
                            InProgressCount = siteGroup.Count(i => i.Status == CompletionStatus.InProgress),
                            DelayedCount = siteGroup.Count(i => i.Status == CompletionStatus.Delayed),
                            NotStartedCount = siteGroup.Count(i => i.Status == CompletionStatus.NotStarted),
                            CompletionRatePercent = siteGroup.Any()
                                ? Math.Round(100.0 * siteGroup.Count(i => i.Status == CompletionStatus.Completed) / siteGroup.Count(), 1)
                                : 0
                        })
                        .OrderBy(s => s.SiteName)
                        .ToList()
                })
                .OrderBy(g => g.InitiativeType)
                .ToList();

            return result;
        }

        // ---- Training (sheet 1): per-site rollup ----
        public async Task<List<TrainingSummaryDto>> GetTrainingSummaryAsync(int reportPeriodId)
        {
            var raw = await _db.TrainingRecords
                .Where(t => t.ReportPeriodId == reportPeriodId)
                .Include(t => t.Site)
                .ToListAsync();

            return raw
                .GroupBy(t => new { t.SiteId, t.Site.Name })
                .Select(g => new TrainingSummaryDto
                {
                    SiteId = g.Key.SiteId,
                    SiteName = g.Key.Name,
                    TotalTrainings = g.Count(),
                    CompletedTrainings = g.Count(t => t.Status == TrainingStatus.Completed),
                    DepartmentsCovered = g.Select(t => t.Department).Distinct().Count()
                })
                .OrderBy(s => s.SiteName)
                .ToList();
        }

        // ---- Cost Savings (sheet 7): per-site rollup ----
        public async Task<List<CostSavingSummaryDto>> GetCostSavingSummaryAsync(int reportPeriodId)
        {
            var raw = await _db.CostSavingInitiatives
                .Where(c => c.ReportPeriodId == reportPeriodId)
                .Include(c => c.Site)
                .ToListAsync();

            return raw
                .GroupBy(c => new { c.SiteId, c.Site.Name })
                .Select(g => new CostSavingSummaryDto
                {
                    SiteId = g.Key.SiteId,
                    SiteName = g.Key.Name,
                    TotalPotentialSavingLacs = g.Sum(c => c.PotentialSavingLacs),
                    ValidatedSavingLacs = g.Where(c => c.ValidatedByFinance).Sum(c => c.PotentialSavingLacs),
                    ProjectCount = g.Count(),
                    CompletedProjectCount = g.Count(c => c.ProjectStatus == ProjectStatus.Completed)
                })
                .OrderByDescending(s => s.TotalPotentialSavingLacs)
                .ToList();
        }

        // ---- Sites that have NOT submitted for this period ----
        public async Task<List<string>> GetSitesNotSubmittedAsync(int reportPeriodId)
        {
            var allSites = await _db.Sites.Where(s => s.IsActive).ToListAsync();
            var submittedSiteIds = await _db.SiteSubmissions
                .Where(ss => ss.ReportPeriodId == reportPeriodId && ss.IsSubmitted)
                .Select(ss => ss.SiteId)
                .ToListAsync();

            return allSites
                .Where(s => !submittedSiteIds.Contains(s.Id))
                .Select(s => s.Name)
                .ToList();
        }

        // ---- Full global report for a given month, all sheets combined ----
        public async Task<MonthlyGlobalReportDto> GetMonthlyGlobalReportAsync(int reportPeriodId)
        {
            var period = await _db.ReportPeriods.FindAsync(reportPeriodId);
            if (period == null) throw new ArgumentException("Report period not found");

            return new MonthlyGlobalReportDto
            {
                PeriodLabel = period.DisplayName,
                Initiatives = await GetInitiativeSummaryAsync(reportPeriodId),
                Training = await GetTrainingSummaryAsync(reportPeriodId),
                CostSavings = await GetCostSavingSummaryAsync(reportPeriodId),
                SitesNotSubmitted = await GetSitesNotSubmittedAsync(reportPeriodId)
            };
        }

        // ---- Month-over-month trend for total potential cost savings (example metric) ----
        public async Task<List<TrendPointDto>> GetCostSavingTrendAsync(int lastNMonths = 6)
        {
            var periods = await _db.ReportPeriods
                .OrderByDescending(rp => rp.Year).ThenByDescending(rp => rp.Month)
                .Take(lastNMonths)
                .ToListAsync();

            var result = new List<TrendPointDto>();
            foreach (var period in periods.OrderBy(p => p.Year).ThenBy(p => p.Month))
            {
                var total = await _db.CostSavingInitiatives
                    .Where(c => c.ReportPeriodId == period.Id)
                    .SumAsync(c => c.PotentialSavingLacs);

                result.Add(new TrendPointDto
                {
                    PeriodLabel = period.DisplayName,
                    Value = (double)total
                });
            }
            return result;
        }
    }
}
