using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SiteReportApp.Data;
using SiteReportApp.Dtos;
using SiteReportApp.Models;

namespace SiteReportApp.Services
{
    public class ScorecardService
    {
        private readonly AppDbContext _db;
        private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNamingPolicy = null };

        public ScorecardService(AppDbContext db) { _db = db; }

        // ---- Schema for the frontend ----
        public List<ScMetricDto> GetSchema() =>
            ScorecardSchema.Metrics.OrderBy(m => m.Order).Select(ToDto).ToList();

        public ScMetricDto? GetMetricSchema(string key)
        {
            var m = ScorecardSchema.Find(key);
            return m == null ? null : ToDto(m);
        }

        private static ScMetricDto ToDto(ScMetric m) => new()
        {
            Key = m.Key, Title = m.Title, Category = m.Category, SheetName = m.SheetName,
            MultiRow = m.MultiRow, Order = m.Order,
            Columns = m.Columns.Select(c => new ScColumnDto
            {
                Key = c.Key, Label = c.Label, Type = c.Type.ToString(), Formula = c.Formula
            }).ToList()
        };

        // ---- Guard: block writes if the period is locked ----
        private async Task EnsurePeriodIsOpenAsync(int reportPeriodId)
        {
            var period = await _db.ReportPeriods.FindAsync(reportPeriodId);
            if (period == null) throw new InvalidOperationException("Report period not found.");
            if (period.Status == ReportPeriodStatus.Locked)
                throw new InvalidOperationException($"Report period {period.DisplayName} is locked and cannot be edited.");
        }

        // ---- Save (replace) a metric's rows for a site/period ----
        // Replace semantics: the incoming set fully defines this site/period/metric, so we
        // delete any existing rows first. This keeps multi-row sheets consistent (no orphan
        // rows after a row is removed in the UI) and makes re-imports idempotent.
        public async Task<int> SaveAsync(ScorecardSaveDto dto)
        {
            await EnsurePeriodIsOpenAsync(dto.ReportPeriodId);
            var metric = ScorecardSchema.Find(dto.MetricKey)
                ?? throw new InvalidOperationException($"Unknown metric '{dto.MetricKey}'.");

            var inputKeys = metric.Columns
                .Where(c => c.Type != ScColType.Computed)
                .Select(c => c.Key)
                .ToHashSet();

            var existing = await _db.ScorecardEntries
                .Where(e => e.SiteId == dto.SiteId && e.ReportPeriodId == dto.ReportPeriodId && e.MetricKey == metric.Key)
                .ToListAsync();
            _db.ScorecardEntries.RemoveRange(existing);

            int saved = 0;
            int idx = 0;
            foreach (var row in dto.Rows)
            {
                // Keep only known input cells; drop computed/unknown keys and fully-blank rows.
                var clean = new Dictionary<string, string?>();
                foreach (var kv in row.Cells)
                {
                    if (inputKeys.Contains(kv.Key) && !string.IsNullOrWhiteSpace(kv.Value))
                        clean[kv.Key] = kv.Value.Trim();
                }
                if (clean.Count == 0) continue;

                _db.ScorecardEntries.Add(new ScorecardEntry
                {
                    SiteId = dto.SiteId,
                    ReportPeriodId = dto.ReportPeriodId,
                    MetricKey = metric.Key,
                    RowIndex = idx++,
                    CellsJson = JsonSerializer.Serialize(clean, JsonOpts)
                });
                saved++;
            }

            await _db.SaveChangesAsync();
            return saved;
        }

        // ---- Read a metric's rows for a site/period, with computed columns resolved ----
        public async Task<List<ScorecardResolvedRowDto>> GetRowsAsync(int siteId, int reportPeriodId, string metricKey)
        {
            var metric = ScorecardSchema.Find(metricKey)
                ?? throw new InvalidOperationException($"Unknown metric '{metricKey}'.");

            var entries = await _db.ScorecardEntries
                .Where(e => e.SiteId == siteId && e.ReportPeriodId == reportPeriodId && e.MetricKey == metric.Key)
                .OrderBy(e => e.RowIndex)
                .ToListAsync();

            return entries.Select(e => ResolveRow(metric, e)).ToList();
        }

        private static ScorecardResolvedRowDto ResolveRow(ScMetric metric, ScorecardEntry e)
        {
            var raw = DeserializeCells(e.CellsJson);
            var computed = ScorecardFormula.ComputeRow(metric, raw);

            var cells = new Dictionary<string, string>();
            foreach (var col in metric.Columns)
            {
                if (col.Type == ScColType.Computed)
                {
                    cells[col.Key] = computed.TryGetValue(col.Key, out var v) && v.HasValue
                        ? FormatNumber(v.Value) : "-";
                }
                else
                {
                    cells[col.Key] = raw.TryGetValue(col.Key, out var s) && s != null ? s : "";
                }
            }
            return new ScorecardResolvedRowDto { Id = e.Id, RowIndex = e.RowIndex, Cells = cells };
        }

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

        private static string FormatNumber(double v)
        {
            // Trim trailing zeros; keep up to 4 dp for ratios.
            var rounded = Math.Round(v, 4);
            return rounded.ToString("0.####", CultureInfo.InvariantCulture);
        }

        // ---- Submission status across all metrics for a site/period ----
        public async Task<Dictionary<string, int>> GetRowCountsAsync(int siteId, int reportPeriodId)
        {
            var grouped = await _db.ScorecardEntries
                .Where(e => e.SiteId == siteId && e.ReportPeriodId == reportPeriodId)
                .GroupBy(e => e.MetricKey)
                .Select(g => new { g.Key, Count = g.Count() })
                .ToListAsync();
            return grouped.ToDictionary(g => g.Key, g => g.Count);
        }

        // ====================================================================
        //  ANALYTICS
        // ====================================================================
        //
        // Returns, for one metric (optionally one column), a flat list of points:
        // (site, period, column, value). The frontend pivots these into:
        //   - month-wise per-site series          (group by site, x = period)
        //   - combined-all-sites series           (group by period, sum/avg across sites)
        //   - single-month site comparison        (filter one period, group by site)
        //   - cross-metric / multi-column views    (request several columns)
        //
        // Value semantics per column:
        //   * Number column  -> SUM of that input across the rows in that site/period
        //   * Computed column-> recomputed from the SUMMED inputs (so e.g. an overall
        //     OOS% across multi-row data is correct, not an average-of-ratios).
        public async Task<List<ScorecardAnalyticsPointDto>> GetAnalyticsAsync(ScorecardAnalyticsQueryDto q)
        {
            var metric = ScorecardSchema.Find(q.MetricKey)
                ?? throw new InvalidOperationException($"Unknown metric '{q.MetricKey}'.");

            // normalise from/to
            int fromKey = q.FromYear * 100 + q.FromMonth;
            int toKey = q.ToYear * 100 + q.ToMonth;
            if (fromKey > toKey)
            {
                (q.FromYear, q.ToYear) = (q.ToYear, q.FromYear);
                (q.FromMonth, q.ToMonth) = (q.ToMonth, q.FromMonth);
            }

            var periods = await _db.ReportPeriods
                .Where(p =>
                    (p.Year > q.FromYear || (p.Year == q.FromYear && p.Month >= q.FromMonth)) &&
                    (p.Year < q.ToYear || (p.Year == q.ToYear && p.Month <= q.ToMonth)))
                .ToListAsync();
            if (periods.Count == 0) return new();
            var periodById = periods.ToDictionary(p => p.Id);
            var periodIds = periods.Select(p => p.Id).ToList();

            var siteFilter = (q.SiteIds != null && q.SiteIds.Count > 0) ? q.SiteIds.ToHashSet() : null;

            var entriesQuery = _db.ScorecardEntries
                .Where(e => e.MetricKey == metric.Key && periodIds.Contains(e.ReportPeriodId))
                .Include(e => e.Site)
                .AsQueryable();

            var entries = await entriesQuery.ToListAsync();
            if (siteFilter != null) entries = entries.Where(e => siteFilter.Contains(e.SiteId)).ToList();

            // Which columns to emit
            var numericCols = metric.Columns
                .Where(c => c.Type == ScColType.Number || c.Type == ScColType.Computed)
                .ToList();
            if (!string.IsNullOrWhiteSpace(q.ColumnKey))
                numericCols = numericCols.Where(c => c.Key.Equals(q.ColumnKey, StringComparison.OrdinalIgnoreCase)).ToList();

            var result = new List<ScorecardAnalyticsPointDto>();

            // Group by (site, period); within a group, sum inputs then recompute ratios.
            foreach (var grp in entries.GroupBy(e => new { e.SiteId, e.ReportPeriodId }))
            {
                var period = periodById[grp.Key.ReportPeriodId];
                var siteName = grp.First().Site?.Name ?? $"Site {grp.Key.SiteId}";

                // Sum input columns across all rows in this group.
                var summedInputs = new Dictionary<string, double?>();
                foreach (var col in metric.Columns.Where(c => c.Type == ScColType.Number))
                {
                    double sum = 0; bool any = false;
                    foreach (var e in grp)
                    {
                        var raw = DeserializeCells(e.CellsJson);
                        if (raw.TryGetValue(col.Key, out var s) && s != null &&
                            double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var n))
                        { sum += n; any = true; }
                    }
                    summedInputs[col.Key] = any ? sum : null;
                }
                // Recompute the computed columns from summed inputs.
                foreach (var col in metric.Columns.Where(c => c.Type == ScColType.Computed && c.Formula != null))
                    summedInputs[col.Key] = ScorecardFormula.Evaluate(col.Formula!, summedInputs);

                foreach (var col in numericCols)
                {
                    summedInputs.TryGetValue(col.Key, out var val);
                    result.Add(new ScorecardAnalyticsPointDto
                    {
                        SiteId = grp.Key.SiteId,
                        SiteName = siteName,
                        ReportPeriodId = period.Id,
                        PeriodLabel = period.DisplayName,
                        Year = period.Year,
                        Month = period.Month,
                        MetricKey = metric.Key,
                        ColumnKey = col.Key,
                        Value = val.HasValue ? Math.Round(val.Value, 4) : (double?)null
                    });
                }
            }

            return result
                .OrderBy(r => r.SiteName).ThenBy(r => r.Year).ThenBy(r => r.Month).ThenBy(r => r.ColumnKey)
                .ToList();
        }
    }
}
