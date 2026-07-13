using Microsoft.EntityFrameworkCore;
using SiteReportApp.Data;
using SiteReportApp.Dtos;
using SiteReportApp.Models;

namespace SiteReportApp.Services
{
    // Owns the site-month submission workflow: the freeze guard used by all
    // scorecard writes, the site's submit action, corporate's review decision,
    // and the corporate overview grid.
    public class DataEntryService
    {
        private readonly AppDbContext _db;

        public DataEntryService(AppDbContext db)
        {
            _db = db;
        }

        // ---- Guard: block writes if the period is locked ----
        private async Task EnsurePeriodIsOpenAsync(int reportPeriodId)
        {
            var period = await _db.ReportPeriods.FindAsync(reportPeriodId);
            if (period == null)
                throw new InvalidOperationException("Report period not found.");
            if (period.Status == ReportPeriodStatus.Locked)
                throw new InvalidOperationException($"Report period {period.DisplayName} is locked and cannot be edited.");
        }

        // ---- Guard: a site's data is frozen while its submission is with corporate ----
        // Editable states: no submission yet, or Returned (corporate sent it back).
        // Frozen states: Submitted (awaiting review) and Approved.
        public async Task EnsureSiteEditableAsync(int siteId, int reportPeriodId)
        {
            await EnsurePeriodIsOpenAsync(reportPeriodId);
            var submission = await _db.SiteSubmissions
                .FirstOrDefaultAsync(ss => ss.SiteId == siteId && ss.ReportPeriodId == reportPeriodId);
            if (submission == null) return;
            if (submission.Status == SubmissionStatus.Submitted)
                throw new InvalidOperationException(
                    "This month has been submitted to corporate and is awaiting review. It cannot be edited unless corporate returns it for revision.");
            if (submission.Status == SubmissionStatus.Approved)
                throw new InvalidOperationException(
                    "This month has been approved by corporate and can no longer be edited.");
        }

        // ---- Workflow: site submits its month to corporate for review ----
        public async Task<SiteSubmission> SubmitAsync(SiteSubmissionCreateDto request)
        {
            var period = await _db.ReportPeriods.FindAsync(request.ReportPeriodId)
                ?? throw new InvalidOperationException("Report period not found.");
            if (period.Status == ReportPeriodStatus.Locked)
                throw new InvalidOperationException($"Report period {period.DisplayName} is locked; submissions are closed.");

            var existing = await _db.SiteSubmissions
                .FirstOrDefaultAsync(ss => ss.SiteId == request.SiteId && ss.ReportPeriodId == request.ReportPeriodId);

            if (existing != null && existing.Status == SubmissionStatus.Approved)
                throw new InvalidOperationException("This month has already been approved by corporate.");
            if (existing != null && existing.Status == SubmissionStatus.Submitted)
                throw new InvalidOperationException("This month is already submitted and awaiting corporate review.");

            if (existing == null)
            {
                existing = new SiteSubmission { SiteId = request.SiteId, ReportPeriodId = request.ReportPeriodId };
                _db.SiteSubmissions.Add(existing);
            }

            existing.Status = SubmissionStatus.Submitted;
            existing.IsSubmitted = true;
            existing.SubmittedAtUtc = DateTime.UtcNow;
            existing.SubmittedBy = request.SubmittedBy;
            // A resubmission clears the previous review outcome. The last review
            // comments are kept on the row as context for the next reviewer.
            existing.ReviewedAtUtc = null;
            existing.ReviewedBy = null;

            await _db.SaveChangesAsync();
            return existing;
        }

        // ---- Workflow: corporate approves or returns a submission ----
        public async Task<SiteSubmission> ReviewAsync(int submissionId, SubmissionReviewDto dto, string reviewer)
        {
            var submission = await _db.SiteSubmissions
                .Include(ss => ss.Site)
                .FirstOrDefaultAsync(ss => ss.Id == submissionId)
                ?? throw new KeyNotFoundException();

            if (submission.Status != SubmissionStatus.Submitted && submission.Status != SubmissionStatus.Approved)
                throw new InvalidOperationException("Only a submitted month can be reviewed.");

            var decision = dto.Decision?.Trim().ToLowerInvariant();
            switch (decision)
            {
                case "approve":
                    submission.Status = SubmissionStatus.Approved;
                    submission.IsSubmitted = true;
                    break;
                case "return":
                    if (string.IsNullOrWhiteSpace(dto.Comments))
                        throw new InvalidOperationException("Comments are required when returning a submission for revision.");
                    submission.Status = SubmissionStatus.Returned;
                    submission.IsSubmitted = false;
                    break;
                default:
                    throw new InvalidOperationException($"Unknown decision '{dto.Decision}'. Use 'Approve' or 'Return'.");
            }

            submission.ReviewedAtUtc = DateTime.UtcNow;
            submission.ReviewedBy = reviewer;
            submission.ReviewComments = string.IsNullOrWhiteSpace(dto.Comments) ? null : dto.Comments.Trim();

            await _db.SaveChangesAsync();
            return submission;
        }

        // ---- Corporate review grid: every active site + workflow state + scorecard fill ----
        public async Task<List<SubmissionOverviewRowDto>> GetOverviewAsync(int reportPeriodId)
        {
            var sites = await _db.Sites.Where(s => s.IsActive).OrderBy(s => s.Name).ToListAsync();
            var submissions = await _db.SiteSubmissions
                .Where(ss => ss.ReportPeriodId == reportPeriodId)
                .ToDictionaryAsync(ss => ss.SiteId);

            var scorecardCounts = await _db.ScorecardEntries
                .Where(e => e.ReportPeriodId == reportPeriodId)
                .GroupBy(e => e.SiteId)
                .Select(g => new { SiteId = g.Key, Count = g.Select(e => e.MetricKey).Distinct().Count() })
                .ToDictionaryAsync(x => x.SiteId, x => x.Count);

            return sites.Select(s =>
            {
                submissions.TryGetValue(s.Id, out var sub);
                return new SubmissionOverviewRowDto
                {
                    SiteId = s.Id,
                    SiteName = s.Name,
                    SiteCode = s.Code,
                    SubmissionId = sub?.Id,
                    Status = (sub?.Status ?? SubmissionStatus.NotStarted).ToString(),
                    SubmittedBy = sub?.SubmittedBy,
                    SubmittedAtUtc = sub?.SubmittedAtUtc,
                    ReviewedBy = sub?.ReviewedBy,
                    ReviewedAtUtc = sub?.ReviewedAtUtc,
                    ReviewComments = sub?.ReviewComments,
                    ScorecardMetricCount = scorecardCounts.GetValueOrDefault(s.Id)
                };
            }).ToList();
        }
    }
}
