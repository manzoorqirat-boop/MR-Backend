using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SiteReportApp.Data;
using SiteReportApp.Dtos;
using SiteReportApp.Models;

namespace SiteReportApp.Services
{
    // ==========================================================================
    //  ANALYTICS SERVICE
    // ==========================================================================
    //  Backs the head-office dashboards (AnalyticsController). Three kinds of
    //  summary here:
    //
    //   - Initiatives: derived from the existing "Equipment Qualification"
    //     scorecard sheet (Initiated / Carry Forward / Completed / In-Progress
    //     activity counts, which are stored as free text in the template so
    //     values are parsed defensively).
    //   - Training: derived from the existing "Training %" scorecard sheet.
    //   - Cost Savings: backed by the new CostSavings table (no scorecard sheet
    //     for this exists yet).
    //
    //  Range/global-report aggregate generically across every metric in
    //  ScorecardSchema, reusing the same "sum inputs, then recompute ratios"
    //  rule as ScorecardService.GetAnalyticsAsync.
    // ==========================================================================
    public class AnalyticsService
    {
        private readonly AppDbContext _db;
        private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNamingPolicy = null };

        public AnalyticsService(AppDbContext db) { _db = db; }

        // ---- shared helpers ----
        private static Dictionary<string, string?> DeserializeCells(string json)
        {
            try
            {
                return JsonSerializer.Deserialize<Dictionary<string, string?>>(json, JsonOpts)
                       ?? new Dictionary<string, string?>();
            }
            catch
            {
                return new Dictionary<string, string?>();
            }
        }

        private static double ParseNumeric(string? raw) =>
            !string.IsNullOrWhiteSpace(raw) &&
            double.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var n) ? n : 0;

        private static double? TryParseNullable(string? raw) =>
            !string.IsNullOrWhiteSpace(raw) &&
            double.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var n) ? n : (double?)null;

        private async Task<ReportPeriod> RequirePeriodAsync(int reportPeriodId)
        {
            var period = await _db.ReportPeriods.FindAsync(reportPeriodId);
            if (period == null) throw new InvalidOperationException($"Report period {reportPeriodId} not found.");
            return period;
        }

        // Sum Number-typed columns across a set of scorecard entries, then recompute
        // Computed columns from those sums (matches ScorecardService's aggregation rule).
        private static Dictionary<string, double?> SumAndRecompute(ScMetric metric, IEnumerable<ScorecardEntry> entries)
        {
            var summed = new Dictionary<string, double?>();
            foreach (var col in metric.Columns.Where(c => c.Type == ScColType.Number))
            {
                double sum = 0; bool any = false;
                foreach (var e in entries)
                {
                    var raw = DeserializeCells(e.CellsJson);
                    if (raw.TryGetValue(col.Key, out var s) && s != null &&
                        double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var n))
                    { sum += n; any = true; }
                }
                summed[col.Key] = any ? sum : null;
            }
            foreach (var col in metric.Columns.Where(c => c.Type == ScColType.Computed && c.Formula != null))
                summed[col.Key] = ScorecardFormula.Evaluate(col.Formula!, summed);
            return summed;
        }

        // Same idea, but re-aggregating from already-summed per-period dictionaries
        // (used to roll monthly sums up into quarters).
        private static Dictionary<string, double?> SumAndRecomputeFromPeriods(
            ScMetric metric, IEnumerable<Dictionary<string, double?>> periodDicts)
        {
            var summed = new Dictionary<string, double?>();
            foreach (var col in metric.Columns.Where(c => c.Type == ScColType.Number))
            {
                double sum = 0; bool any = false;
                foreach (var d in periodDicts)
                    if (d.TryGetValue(col.Key, out var v) && v.HasValue) { sum += v.Value; any = true; }
                summed[col.Key] = any ? sum : null;
            }
            foreach (var col in metric.Columns.Where(c => c.Type == ScColType.Computed && c.Formula != null))
                summed[col.Key] = ScorecardFormula.Evaluate(col.Formula!, summed);
            return summed;
        }

        // ======================================================================
        //  Initiatives (Equipment Qualification sheet)
        // ======================================================================
        public async Task<InitiativeSummaryDto> GetInitiativeSummaryAsync(int reportPeriodId)
        {
            var period = await RequirePeriodAsync(reportPeriodId);
            var metric = ScorecardSchema.Find("equipmentQualification")
                ?? throw new InvalidOperationException("equipmentQualification metric not found in schema.");

            var entries = await _db.ScorecardEntries
                .Where(e => e.ReportPeriodId == reportPeriodId && e.MetricKey == metric.Key)
                .Include(e => e.Site)
                .ToListAsync();

            var dto = new InitiativeSummaryDto { ReportPeriodId = period.Id, PeriodLabel = period.DisplayName };
            var byType = new Dictionary<(string Type, string Segment), InitiativeTypeSummaryDto>();
            var bySite = new Dictionary<int, InitiativeSiteSummaryDto>();

            foreach (var e in entries)
            {
                var cells = DeserializeCells(e.CellsJson);
                var type = cells.GetValueOrDefault("typeOfActivities") ?? "";
                var segment = cells.GetValueOrDefault("productSegment") ?? "";
                var initiated = ParseNumeric(cells.GetValueOrDefault("noSActivitiesInitiatedReportingMonth"));
                var carry = ParseNumeric(cells.GetValueOrDefault("carryForwardFromPreviousMonthS"));
                var completed = ParseNumeric(cells.GetValueOrDefault("activitiesCompleted"));
                var inProgress = ParseNumeric(cells.GetValueOrDefault("activitiesUProgress"));

                dto.TotalInitiated += initiated;
                dto.TotalCarryForward += carry;
                dto.TotalCompleted += completed;
                dto.TotalInProgress += inProgress;

                var typeKey = (type, segment);
                if (!byType.TryGetValue(typeKey, out var t))
                    byType[typeKey] = t = new InitiativeTypeSummaryDto { TypeOfActivities = type, ProductSegment = segment };
                t.Initiated += initiated; t.CarryForward += carry; t.Completed += completed; t.InProgress += inProgress;

                if (!bySite.TryGetValue(e.SiteId, out var s))
                    bySite[e.SiteId] = s = new InitiativeSiteSummaryDto
                    {
                        SiteId = e.SiteId,
                        SiteName = e.Site?.Name ?? $"Site {e.SiteId}"
                    };
                s.Initiated += initiated; s.CarryForward += carry; s.Completed += completed; s.InProgress += inProgress;
            }

            dto.ByType = byType.Values.OrderBy(x => x.TypeOfActivities).ThenBy(x => x.ProductSegment).ToList();
            dto.BySite = bySite.Values.OrderBy(x => x.SiteName).ToList();
            return dto;
        }

        // ======================================================================
        //  Training (Training % sheet)
        // ======================================================================
        public async Task<TrainingSummaryDto> GetTrainingSummaryAsync(int reportPeriodId)
        {
            var period = await RequirePeriodAsync(reportPeriodId);
            var metric = ScorecardSchema.Find("training")
                ?? throw new InvalidOperationException("training metric not found in schema.");

            var entries = await _db.ScorecardEntries
                .Where(e => e.ReportPeriodId == reportPeriodId && e.MetricKey == metric.Key)
                .Include(e => e.Site)
                .ToListAsync();

            var dto = new TrainingSummaryDto { ReportPeriodId = period.Id, PeriodLabel = period.DisplayName };
            var sopVals = new List<double>();
            var gmpVals = new List<double>();
            var funcVals = new List<double>();

            foreach (var e in entries)
            {
                var cells = DeserializeCells(e.CellsJson);
                var sop = TryParseNullable(cells.GetValueOrDefault("completionOfSopTraining"));
                var gmp = TryParseNullable(cells.GetValueOrDefault("completionOfGmpTraining"));
                var func = TryParseNullable(cells.GetValueOrDefault("completionOfFuntionalTraining"));
                var external = ParseNumeric(cells.GetValueOrDefault("noOfExternalTrainingByOem"));

                if (sop.HasValue) sopVals.Add(sop.Value);
                if (gmp.HasValue) gmpVals.Add(gmp.Value);
                if (func.HasValue) funcVals.Add(func.Value);
                dto.TotalExternalTrainings += external;

                dto.Sites.Add(new TrainingSiteSummaryDto
                {
                    SiteId = e.SiteId,
                    SiteName = e.Site?.Name ?? $"Site {e.SiteId}",
                    SopCompletionPct = sop,
                    GmpCompletionPct = gmp,
                    FunctionalCompletionPct = func,
                    ExternalTrainingCount = external
                });
            }

            dto.AvgSopCompletionPct = sopVals.Count > 0 ? Math.Round(sopVals.Average(), 2) : null;
            dto.AvgGmpCompletionPct = gmpVals.Count > 0 ? Math.Round(gmpVals.Average(), 2) : null;
            dto.AvgFunctionalCompletionPct = funcVals.Count > 0 ? Math.Round(funcVals.Average(), 2) : null;
            dto.Sites = dto.Sites.OrderBy(s => s.SiteName).ToList();
            return dto;
        }

        // ======================================================================
        //  Cost Savings (new CostSavings table)
        // ======================================================================
        public async Task<CostSaving> AddCostSavingAsync(CostSavingCreateDto dto, string createdBy)
        {
            if (!await _db.Sites.AnyAsync(s => s.Id == dto.SiteId))
                throw new InvalidOperationException("Site does not exist.");
            if (!await _db.ReportPeriods.AnyAsync(p => p.Id == dto.ReportPeriodId))
                throw new InvalidOperationException("Report period does not exist.");
            if (dto.AmountSaved < 0)
                throw new InvalidOperationException("Amount saved cannot be negative.");

            var entity = new CostSaving
            {
                SiteId = dto.SiteId,
                ReportPeriodId = dto.ReportPeriodId,
                AmountSaved = dto.AmountSaved,
                Description = dto.Description?.Trim() ?? "",
                CreatedBy = createdBy,
                CreatedAtUtc = DateTime.UtcNow
            };
            _db.CostSavings.Add(entity);
            await _db.SaveChangesAsync();
            return entity;
        }

        public async Task<CostSavingSummaryDto> GetCostSavingSummaryAsync(int reportPeriodId)
        {
            var period = await RequirePeriodAsync(reportPeriodId);
            var items = await _db.CostSavings
                .Where(c => c.ReportPeriodId == reportPeriodId)
                .Include(c => c.Site)
                .ToListAsync();

            return new CostSavingSummaryDto
            {
                ReportPeriodId = period.Id,
                PeriodLabel = period.DisplayName,
                TotalSaved = items.Sum(i => i.AmountSaved),
                BySite = items.GroupBy(i => i.SiteId)
                    .Select(g => new CostSavingSiteSummaryDto
                    {
                        SiteId = g.Key,
                        SiteName = g.First().Site?.Name ?? $"Site {g.Key}",
                        TotalSaved = g.Sum(x => x.AmountSaved),
                        ItemCount = g.Count()
                    })
                    .OrderBy(s => s.SiteName)
                    .ToList()
            };
        }

        public async Task<List<CostSavingTrendPointDto>> GetCostSavingTrendAsync(int lastNMonths)
        {
            if (lastNMonths <= 0) lastNMonths = 6;

            var periods = await _db.ReportPeriods
                .OrderByDescending(p => p.Year).ThenByDescending(p => p.Month)
                .Take(lastNMonths)
                .ToListAsync();
            var periodIds = periods.Select(p => p.Id).ToList();

            var sums = await _db.CostSavings
                .Where(c => periodIds.Contains(c.ReportPeriodId))
                .GroupBy(c => c.ReportPeriodId)
                .Select(g => new { ReportPeriodId = g.Key, Total = g.Sum(x => x.AmountSaved) })
                .ToListAsync();
            var sumByPeriod = sums.ToDictionary(s => s.ReportPeriodId, s => s.Total);

            return periods
                .OrderBy(p => p.Year).ThenBy(p => p.Month)
                .Select(p => new CostSavingTrendPointDto
                {
                    ReportPeriodId = p.Id,
                    PeriodLabel = p.DisplayName,
                    Year = p.Year,
                    Month = p.Month,
                    TotalSaved = sumByPeriod.TryGetValue(p.Id, out var t) ? t : 0m
                })
                .ToList();
        }

        // ======================================================================
        //  Range analytics — generic aggregation across every scorecard metric
        // ======================================================================
        public async Task<RangeAnalyticsDto> GetRangeAnalyticsAsync(
            int fromYear, int fromMonth, int toYear, int toMonth, string granularity, int? siteId)
        {
            var g = (granularity ?? "monthly").Trim().ToLowerInvariant();
            if (g != "monthly" && g != "quarterly") g = "monthly";

            if (fromYear * 100 + fromMonth > toYear * 100 + toMonth)
            {
                (fromYear, toYear) = (toYear, fromYear);
                (fromMonth, toMonth) = (toMonth, fromMonth);
            }

            var periods = await _db.ReportPeriods
                .Where(p =>
                    (p.Year > fromYear || (p.Year == fromYear && p.Month >= fromMonth)) &&
                    (p.Year < toYear || (p.Year == toYear && p.Month <= toMonth)))
                .ToListAsync();

            var result = new RangeAnalyticsDto { Granularity = g, SiteId = siteId };
            if (periods.Count == 0) return result;

            var periodById = periods.ToDictionary(p => p.Id);
            var periodIds = periods.Select(p => p.Id).ToList();

            foreach (var metric in ScorecardSchema.Metrics.OrderBy(m => m.Order))
            {
                var numericCols = metric.Columns.Where(c => c.Type == ScColType.Number || c.Type == ScColType.Computed).ToList();
                if (numericCols.Count == 0) continue;

                var entriesQuery = _db.ScorecardEntries
                    .Where(e => e.MetricKey == metric.Key && periodIds.Contains(e.ReportPeriodId));
                if (siteId.HasValue)
                    entriesQuery = entriesQuery.Where(e => e.SiteId == siteId.Value);

                var entries = await entriesQuery.ToListAsync();
                if (entries.Count == 0) continue;

                // Sum inputs per calendar month first (across sites, unless siteId narrows it).
                var perPeriod = entries
                    .GroupBy(e => e.ReportPeriodId)
                    .ToDictionary(grp => grp.Key, grp => SumAndRecompute(metric, grp));

                if (g == "monthly")
                {
                    foreach (var (periodId, values) in perPeriod)
                    {
                        var period = periodById[periodId];
                        foreach (var col in numericCols)
                        {
                            values.TryGetValue(col.Key, out var val);
                            result.Points.Add(new RangeMetricPointDto
                            {
                                MetricKey = metric.Key,
                                MetricTitle = metric.Title,
                                Category = metric.Category,
                                ColumnKey = col.Key,
                                ColumnLabel = col.Label,
                                PeriodLabel = period.DisplayName,
                                Year = period.Year,
                                Month = period.Month,
                                Value = val.HasValue ? Math.Round(val.Value, 4) : (double?)null
                            });
                        }
                    }
                }
                else // quarterly: roll the monthly sums up into their quarter, then recompute
                {
                    var byQuarter = perPeriod.Keys
                        .GroupBy(pid => (Year: periodById[pid].Year, Quarter: (periodById[pid].Month - 1) / 3 + 1));

                    foreach (var qGrp in byQuarter)
                    {
                        var quarterValues = SumAndRecomputeFromPeriods(metric, qGrp.Select(pid => perPeriod[pid]));
                        foreach (var col in numericCols)
                        {
                            quarterValues.TryGetValue(col.Key, out var val);
                            result.Points.Add(new RangeMetricPointDto
                            {
                                MetricKey = metric.Key,
                                MetricTitle = metric.Title,
                                Category = metric.Category,
                                ColumnKey = col.Key,
                                ColumnLabel = col.Label,
                                PeriodLabel = $"{qGrp.Key.Year}-Q{qGrp.Key.Quarter}",
                                Year = qGrp.Key.Year,
                                Quarter = qGrp.Key.Quarter,
                                Value = val.HasValue ? Math.Round(val.Value, 4) : (double?)null
                            });
                        }
                    }
                }
            }

            result.Points = result.Points
                .OrderBy(p => p.Category).ThenBy(p => p.MetricTitle)
                .ThenBy(p => p.Year).ThenBy(p => p.Month).ThenBy(p => p.Quarter).ThenBy(p => p.ColumnKey)
                .ToList();
            return result;
        }

        // ======================================================================
        //  Monthly Global Report — single period, every metric, per-site + overall
        // ======================================================================
        public async Task<GlobalReportDto> GetMonthlyGlobalReportAsync(int reportPeriodId)
        {
            var period = await RequirePeriodAsync(reportPeriodId);
            var dto = new GlobalReportDto { ReportPeriodId = period.Id, PeriodLabel = period.DisplayName };

            dto.Initiatives = await GetInitiativeSummaryAsync(reportPeriodId);
            dto.Training = await GetTrainingSummaryAsync(reportPeriodId);
            dto.CostSavings = await GetCostSavingSummaryAsync(reportPeriodId);

            var sites = await _db.Sites.ToDictionaryAsync(s => s.Id, s => s.Name);

            var categories = new List<GlobalReportCategoryDto>();
            foreach (var catGrp in ScorecardSchema.Metrics.OrderBy(m => m.Order).GroupBy(m => m.Category))
            {
                var catDto = new GlobalReportCategoryDto { Category = catGrp.Key };

                foreach (var metric in catGrp)
                {
                    var numericCols = metric.Columns.Where(c => c.Type == ScColType.Number || c.Type == ScColType.Computed).ToList();
                    if (numericCols.Count == 0) continue;

                    var entries = await _db.ScorecardEntries
                        .Where(e => e.ReportPeriodId == reportPeriodId && e.MetricKey == metric.Key)
                        .ToListAsync();
                    if (entries.Count == 0) continue;

                    var metricDto = new GlobalReportMetricDto { MetricKey = metric.Key, Title = metric.Title, Category = metric.Category };

                    var overall = SumAndRecompute(metric, entries);
                    metricDto.Overall = numericCols.Select(c => new GlobalReportColumnValueDto
                    {
                        ColumnKey = c.Key,
                        ColumnLabel = c.Label,
                        Value = overall.TryGetValue(c.Key, out var v) && v.HasValue ? Math.Round(v.Value, 4) : (double?)null
                    }).ToList();

                    foreach (var siteGrp in entries.GroupBy(e => e.SiteId))
                    {
                        var summed = SumAndRecompute(metric, siteGrp);
                        metricDto.BySite.Add(new GlobalReportSiteRowDto
                        {
                            SiteId = siteGrp.Key,
                            SiteName = sites.TryGetValue(siteGrp.Key, out var nm) ? nm : $"Site {siteGrp.Key}",
                            Columns = numericCols.Select(c => new GlobalReportColumnValueDto
                            {
                                ColumnKey = c.Key,
                                ColumnLabel = c.Label,
                                Value = summed.TryGetValue(c.Key, out var v) && v.HasValue ? Math.Round(v.Value, 4) : (double?)null
                            }).ToList()
                        });
                    }
                    metricDto.BySite = metricDto.BySite.OrderBy(s => s.SiteName).ToList();

                    catDto.Metrics.Add(metricDto);
                }

                if (catDto.Metrics.Count > 0) categories.Add(catDto);
            }

            dto.Categories = categories;
            return dto;
        }
    }
}
