using Microsoft.AspNetCore.Mvc;
using SiteReportApp.Services;

namespace SiteReportApp.Controllers
{
    [ApiController]
    [Route("api/analytics")]
    public class AnalyticsController : ControllerBase
    {
        private readonly AnalyticsService _analytics;

        public AnalyticsController(AnalyticsService analytics)
        {
            _analytics = analytics;
        }

        // GET /api/analytics/initiatives?reportPeriodId=5
        [HttpGet("initiatives")]
        public async Task<IActionResult> GetInitiatives([FromQuery] int reportPeriodId)
        {
            var data = await _analytics.GetInitiativeSummaryAsync(reportPeriodId);
            return Ok(data);
        }

        // GET /api/analytics/training?reportPeriodId=5
        [HttpGet("training")]
        public async Task<IActionResult> GetTraining([FromQuery] int reportPeriodId)
        {
            var data = await _analytics.GetTrainingSummaryAsync(reportPeriodId);
            return Ok(data);
        }

        // GET /api/analytics/cost-savings?reportPeriodId=5
        [HttpGet("cost-savings")]
        public async Task<IActionResult> GetCostSavings([FromQuery] int reportPeriodId)
        {
            var data = await _analytics.GetCostSavingSummaryAsync(reportPeriodId);
            return Ok(data);
        }

        // GET /api/analytics/cost-savings/trend?lastNMonths=6
        [HttpGet("cost-savings/trend")]
        public async Task<IActionResult> GetCostSavingTrend([FromQuery] int lastNMonths = 6)
        {
            var data = await _analytics.GetCostSavingTrendAsync(lastNMonths);
            return Ok(data);
        }

        // GET /api/analytics/range?fromYear=2025&fromMonth=1&toYear=2025&toMonth=6&granularity=monthly&siteId=2
        // granularity: "monthly" (default) or "quarterly". siteId optional (omit for all sites).
        // Powers the Analytics page: KPI cards, time-series charts, by-type / status / by-site breakdowns.
        [HttpGet("range")]
        public async Task<IActionResult> GetRange(
            [FromQuery] int fromYear,
            [FromQuery] int fromMonth,
            [FromQuery] int toYear,
            [FromQuery] int toMonth,
            [FromQuery] string granularity = "monthly",
            [FromQuery] int? siteId = null)
        {
            if (fromMonth < 1 || fromMonth > 12 || toMonth < 1 || toMonth > 12)
                return BadRequest(new { error = "Month must be between 1 and 12." });
            if (fromYear < 2000 || toYear < 2000)
                return BadRequest(new { error = "Year looks invalid." });

            var g = (granularity ?? "monthly").Trim().ToLowerInvariant();
            if (g != "monthly" && g != "quarterly")
                return BadRequest(new { error = "granularity must be 'monthly' or 'quarterly'." });

            var data = await _analytics.GetRangeAnalyticsAsync(fromYear, fromMonth, toYear, toMonth, g, siteId);
            return Ok(data);
        }

        // GET /api/analytics/global-report?reportPeriodId=5
        // This single endpoint feeds the head-office "Monthly Global Report" screen/export
        [HttpGet("global-report")]
        public async Task<IActionResult> GetGlobalReport([FromQuery] int reportPeriodId)
        {
            var data = await _analytics.GetMonthlyGlobalReportAsync(reportPeriodId);
            return Ok(data);
        }
    }
}
