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

        // ---- Range analytics: aggregate across a span of periods (monthly or quarterly) ----
        public async Task<AnalyticsRangeDto> GetRangeAnalyticsAsync(
            int fromYear, int fromMonth, int toYear, int toMonth, string granularity, int? siteId)
        {
            // Normalise so "from" is never after "to".
            int fromKey = fromYear * 100 + fromMonth;
            int toKey = toYear * 100 + toMonth;
            if (fromKey > toKey)
            {
                (fromYear, toYear) = (toYear, fromYear);
                (fromMonth, toMonth) = (toMonth, fromMonth);
            }

            bool quarterly = string.Equals(granularity, "quarterly", StringComparison.OrdinalIgnoreCase);

            // Periods whose (year, month) fall inside the inclusive range.
            var periods = await _db.ReportPeriods
                .Where(p =>
                    (p.Year > fromYear || (p.Year == fromYear && p.Month >= fromMonth)) &&
                    (p.Year < toYear || (p.Year == toYear && p.Month <= toMonth)))
                .ToListAsync();

            var dto = new AnalyticsRangeDto
            {
                FromLabel = $"{fromYear}-{fromMonth:D2}",
                ToLabel = $"{toYear}-{toMonth:D2}",
                Granularity = quarterly ? "quarterly" : "monthly",
                SiteId = siteId
            };
            dto.Kpis.PeriodsCount = periods.Count;
            if (periods.Count == 0) return dto;

            var periodIds = periods.Select(p => p.Id).ToList();

            // periodId -> bucket (label + sortKey)
            (string label, int sortKey) BucketOf(int year, int month)
            {
                if (quarterly)
                {
                    int q = (month - 1) / 3 + 1;
                    return ($"{year}-Q{q}", year * 10 + q);
                }
                return ($"{year}-{month:D2}", year * 100 + month);
            }
            var periodBucketKey = periods.ToDictionary(p => p.Id, p => BucketOf(p.Year, p.Month).sortKey);

            // Pre-seed a bucket for every period in range, so months with no data still show as zero.
            var buckets = new Dictionary<int, TimeBucketDto>();
            foreach (var p in periods)
            {
                var (label, key) = BucketOf(p.Year, p.Month);
                if (!buckets.ContainsKey(key))
                    buckets[key] = new TimeBucketDto { Label = label, SortKey = key };
            }

            // Load the three datasets for the range across ALL sites. The site filter is
            // applied in memory below so the per-site comparison can always span every site,
            // even when the rest of the page is focused on one site.
            var allInitiatives = await _db.Initiatives
                .Where(i => periodIds.Contains(i.ReportPeriodId))
                .Include(i => i.Site)
                .ToListAsync();
            var allTrainings = await _db.TrainingRecords
                .Where(t => periodIds.Contains(t.ReportPeriodId))
                .Include(t => t.Site)
                .ToListAsync();
            var allCostSavings = await _db.CostSavingInitiatives
                .Where(c => periodIds.Contains(c.ReportPeriodId))
                .Include(c => c.Site)
                .ToListAsync();

            // Focused subset: everything (all sites) or just the selected site.
            var initiatives = siteId == null ? allInitiatives : allInitiatives.Where(i => i.SiteId == siteId).ToList();
            var trainings = siteId == null ? allTrainings : allTrainings.Where(t => t.SiteId == siteId).ToList();
            var costSavings = siteId == null ? allCostSavings : allCostSavings.Where(c => c.SiteId == siteId).ToList();

            // ---- Per-bucket accumulation (time series) ----
            foreach (var i in initiatives)
            {
                var b = buckets[periodBucketKey[i.ReportPeriodId]];
                b.InitiativesTotal++;
                if (i.Status == CompletionStatus.Completed) b.InitiativesCompleted++;
            }
            foreach (var t in trainings)
            {
                var b = buckets[periodBucketKey[t.ReportPeriodId]];
                b.TrainingsTotal++;
                if (t.Status == TrainingStatus.Completed) b.TrainingsCompleted++;
            }
            foreach (var c in costSavings)
            {
                var b = buckets[periodBucketKey[c.ReportPeriodId]];
                b.CostSavingsPotentialLacs += c.PotentialSavingLacs;
                if (c.ValidatedByFinance) b.CostSavingsValidatedLacs += c.PotentialSavingLacs;
            }
            foreach (var b in buckets.Values)
            {
                b.InitiativeCompletionRate = b.InitiativesTotal > 0
                    ? Math.Round(100.0 * b.InitiativesCompleted / b.InitiativesTotal, 1) : 0;
            }
            dto.Buckets = buckets.Values.OrderBy(b => b.SortKey).ToList();

            // ---- KPI totals across the whole range ----
            dto.Kpis.InitiativesTotal = initiatives.Count;
            dto.Kpis.InitiativesCompleted = initiatives.Count(i => i.Status == CompletionStatus.Completed);
            dto.Kpis.InitiativeCompletionRate = dto.Kpis.InitiativesTotal > 0
                ? Math.Round(100.0 * dto.Kpis.InitiativesCompleted / dto.Kpis.InitiativesTotal, 1) : 0;
            dto.Kpis.TrainingsTotal = trainings.Count;
            dto.Kpis.TrainingsCompleted = trainings.Count(t => t.Status == TrainingStatus.Completed);
            dto.Kpis.TrainingCompletionRate = dto.Kpis.TrainingsTotal > 0
                ? Math.Round(100.0 * dto.Kpis.TrainingsCompleted / dto.Kpis.TrainingsTotal, 1) : 0;
            dto.Kpis.CostSavingsPotentialLacs = costSavings.Sum(c => c.PotentialSavingLacs);
            dto.Kpis.CostSavingsValidatedLacs = costSavings.Where(c => c.ValidatedByFinance).Sum(c => c.PotentialSavingLacs);

            // ---- Initiatives grouped by type ----
            dto.InitiativesByType = initiatives
                .GroupBy(i => i.Type)
                .Select(g => new InitiativeTypeAggregateDto
                {
                    Type = g.Key.ToString(),
                    Total = g.Count(),
                    Completed = g.Count(i => i.Status == CompletionStatus.Completed),
                    CompletionRate = g.Any() ? Math.Round(100.0 * g.Count(i => i.Status == CompletionStatus.Completed) / g.Count(), 1) : 0
                })
                .OrderBy(x => x.Type)
                .ToList();

            // ---- Initiative status breakdown (for a donut) ----
            dto.InitiativeStatusBreakdown = initiatives
                .GroupBy(i => i.Status)
                .Select(g => new StatusBreakdownDto { Status = g.Key.ToString(), Count = g.Count() })
                .OrderBy(s => s.Status)
                .ToList();

            // ---- Per-site rollup across all three datasets (always all sites) ----
            var siteAgg = new Dictionary<int, SiteAggregateDto>();
            SiteAggregateDto SiteRow(int id, string name)
            {
                if (!siteAgg.TryGetValue(id, out var row))
                {
                    row = new SiteAggregateDto { SiteId = id, SiteName = name };
                    siteAgg[id] = row;
                }
                return row;
            }
            foreach (var i in allInitiatives)
            {
                var row = SiteRow(i.SiteId, i.Site?.Name ?? $"Site {i.SiteId}");
                row.InitiativesTotal++;
                switch (i.Status)
                {
                    case CompletionStatus.Completed: row.InitiativesCompleted++; break;
                    case CompletionStatus.InProgress: row.InitiativesInProgress++; break;
                    case CompletionStatus.Delayed: row.InitiativesDelayed++; break;
                    case CompletionStatus.NotStarted: row.InitiativesNotStarted++; break;
                }
            }
            foreach (var t in allTrainings)
                SiteRow(t.SiteId, t.Site?.Name ?? $"Site {t.SiteId}").TrainingsTotal++;
            foreach (var c in allCostSavings)
            {
                var row = SiteRow(c.SiteId, c.Site?.Name ?? $"Site {c.SiteId}");
                row.CostSavingsPotentialLacs += c.PotentialSavingLacs;
                if (c.ValidatedByFinance) row.CostSavingsValidatedLacs += c.PotentialSavingLacs;
            }
            foreach (var row in siteAgg.Values)
            {
                row.InitiativeCompletionRate = row.InitiativesTotal > 0
                    ? Math.Round(100.0 * row.InitiativesCompleted / row.InitiativesTotal, 1) : 0;
            }
            dto.BySite = siteAgg.Values.OrderBy(s => s.SiteName).ToList();

            return dto;
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
