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
