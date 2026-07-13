using Microsoft.EntityFrameworkCore;
using SiteReportApp.Data;
using SiteReportApp.Dtos;
using SiteReportApp.Models;

namespace SiteReportApp.Services
{
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

        // ---- Initiatives (sheets 2-6) ----
        // Upsert by natural key (SiteId, ReportPeriodId, Type, SerialNo): existing rows are
        // updated in place, new serials are inserted. This makes re-imports and double-submits
        // idempotent instead of creating duplicate rows.
        public async Task<ImportResultDto> SaveInitiativesAsync(InitiativeBulkCreateDto request)
        {
            await EnsureSiteEditableAsync(request.SiteId, request.ReportPeriodId);

            var result = new ImportResultDto();
            if (!Enum.TryParse<InitiativeType>(request.Type, ignoreCase: true, out var parsedType))
            {
                result.Errors.Add($"Invalid initiative type: '{request.Type}'");
                result.RowsRejected = request.Rows.Count;
                return result;
            }

            var existing = await _db.Initiatives
                .Where(i => i.SiteId == request.SiteId
                         && i.ReportPeriodId == request.ReportPeriodId
                         && i.Type == parsedType)
                .ToListAsync();
            var bySerial = existing.ToDictionary(i => i.SerialNo);
            var seenSerials = new HashSet<int>();

            for (int i = 0; i < request.Rows.Count; i++)
            {
                var row = request.Rows[i];
                if (!Enum.TryParse<CompletionStatus>(row.Status, ignoreCase: true, out var status))
                {
                    result.Errors.Add($"Row {i + 1}: invalid status '{row.Status}'");
                    result.RowsRejected++;
                    continue;
                }
                if (string.IsNullOrWhiteSpace(row.Name) || string.IsNullOrWhiteSpace(row.Department))
                {
                    result.Errors.Add($"Row {i + 1}: Name and Department are required");
                    result.RowsRejected++;
                    continue;
                }
                if (!seenSerials.Add(row.SerialNo))
                {
                    result.Errors.Add($"Row {i + 1}: duplicate serial no {row.SerialNo} in this upload");
                    result.RowsRejected++;
                    continue;
                }

                if (bySerial.TryGetValue(row.SerialNo, out var entity))
                {
                    entity.Name = row.Name;
                    entity.Department = row.Department;
                    entity.Category = row.Category;
                    entity.FacilitatorName = row.FacilitatorName;
                    entity.DepartmentHead = row.DepartmentHead;
                    entity.Status = status;
                    entity.Remarks = row.Remarks;
                }
                else
                {
                    _db.Initiatives.Add(new Initiative
                    {
                        SiteId = request.SiteId,
                        ReportPeriodId = request.ReportPeriodId,
                        Type = parsedType,
                        SerialNo = row.SerialNo,
                        Name = row.Name,
                        Department = row.Department,
                        Category = row.Category,
                        FacilitatorName = row.FacilitatorName,
                        DepartmentHead = row.DepartmentHead,
                        Status = status,
                        Remarks = row.Remarks
                    });
                }
                result.RowsAccepted++;
            }

            await _db.SaveChangesAsync();
            return result;
        }

        // ---- Training (sheet 1) ----
        public async Task<ImportResultDto> SaveTrainingAsync(TrainingBulkCreateDto request)
        {
            await EnsureSiteEditableAsync(request.SiteId, request.ReportPeriodId);

            var result = new ImportResultDto();

            var existing = await _db.TrainingRecords
                .Where(t => t.SiteId == request.SiteId && t.ReportPeriodId == request.ReportPeriodId)
                .ToListAsync();
            var bySerial = existing.ToDictionary(t => t.SerialNo);
            var seenSerials = new HashSet<int>();

            for (int i = 0; i < request.Rows.Count; i++)
            {
                var row = request.Rows[i];
                if (!Enum.TryParse<TrainingStatus>(row.Status, ignoreCase: true, out var status))
                {
                    result.Errors.Add($"Row {i + 1}: invalid status '{row.Status}'");
                    result.RowsRejected++;
                    continue;
                }
                if (string.IsNullOrWhiteSpace(row.Topic) || string.IsNullOrWhiteSpace(row.Department))
                {
                    result.Errors.Add($"Row {i + 1}: Topic and Department are required");
                    result.RowsRejected++;
                    continue;
                }
                if (!seenSerials.Add(row.SerialNo))
                {
                    result.Errors.Add($"Row {i + 1}: duplicate serial no {row.SerialNo} in this upload");
                    result.RowsRejected++;
                    continue;
                }

                if (bySerial.TryGetValue(row.SerialNo, out var entity))
                {
                    entity.Topic = row.Topic;
                    entity.TrainingImpartedBy = row.TrainingImpartedBy;
                    entity.Department = row.Department;
                    entity.Status = status;
                }
                else
                {
                    _db.TrainingRecords.Add(new TrainingRecord
                    {
                        SiteId = request.SiteId,
                        ReportPeriodId = request.ReportPeriodId,
                        SerialNo = row.SerialNo,
                        Topic = row.Topic,
                        TrainingImpartedBy = row.TrainingImpartedBy,
                        Department = row.Department,
                        Status = status
                    });
                }
                result.RowsAccepted++;
            }

            await _db.SaveChangesAsync();
            return result;
        }

        // ---- Cost Savings (sheet 7) ----
        public async Task<ImportResultDto> SaveCostSavingsAsync(CostSavingBulkCreateDto request)
        {
            await EnsureSiteEditableAsync(request.SiteId, request.ReportPeriodId);

            var result = new ImportResultDto();

            var existing = await _db.CostSavingInitiatives
                .Where(c => c.SiteId == request.SiteId && c.ReportPeriodId == request.ReportPeriodId)
                .ToListAsync();
            var bySerial = existing.ToDictionary(c => c.SerialNo);
            var seenSerials = new HashSet<int>();

            for (int i = 0; i < request.Rows.Count; i++)
            {
                var row = request.Rows[i];
                if (!Enum.TryParse<ProjectStatus>(row.ProjectStatus, ignoreCase: true, out var status))
                {
                    result.Errors.Add($"Row {i + 1}: invalid project status '{row.ProjectStatus}'");
                    result.RowsRejected++;
                    continue;
                }
                if (string.IsNullOrWhiteSpace(row.ProjectName))
                {
                    result.Errors.Add($"Row {i + 1}: Project name is required");
                    result.RowsRejected++;
                    continue;
                }
                if (row.PotentialSavingLacs < 0)
                {
                    result.Errors.Add($"Row {i + 1}: Potential saving cannot be negative");
                    result.RowsRejected++;
                    continue;
                }
                if (!seenSerials.Add(row.SerialNo))
                {
                    result.Errors.Add($"Row {i + 1}: duplicate serial no {row.SerialNo} in this upload");
                    result.RowsRejected++;
                    continue;
                }

                if (bySerial.TryGetValue(row.SerialNo, out var entity))
                {
                    entity.ProjectName = row.ProjectName;
                    entity.PotentialSavingLacs = row.PotentialSavingLacs;
                    entity.ProjectStatus = status;
                    entity.ValidatedByFinance = row.ValidatedByFinance;
                    entity.Remarks = row.Remarks;
                }
                else
                {
                    _db.CostSavingInitiatives.Add(new CostSavingInitiative
                    {
                        SiteId = request.SiteId,
                        ReportPeriodId = request.ReportPeriodId,
                        SerialNo = row.SerialNo,
                        ProjectName = row.ProjectName,
                        PotentialSavingLacs = row.PotentialSavingLacs,
                        ProjectStatus = status,
                        ValidatedByFinance = row.ValidatedByFinance,
                        Remarks = row.Remarks
                    });
                }
                result.RowsAccepted++;
            }

            await _db.SaveChangesAsync();
            return result;
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

        // ---- Corporate review grid: every active site + workflow state + data counts ----
        public async Task<List<SubmissionOverviewRowDto>> GetOverviewAsync(int reportPeriodId)
        {
            var sites = await _db.Sites.Where(s => s.IsActive).OrderBy(s => s.Name).ToListAsync();
            var submissions = await _db.SiteSubmissions
                .Where(ss => ss.ReportPeriodId == reportPeriodId)
                .ToDictionaryAsync(ss => ss.SiteId);

            var trainingCounts = await _db.TrainingRecords
                .Where(t => t.ReportPeriodId == reportPeriodId)
                .GroupBy(t => t.SiteId)
                .Select(g => new { SiteId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.SiteId, x => x.Count);

            var initiativeCounts = await _db.Initiatives
                .Where(i => i.ReportPeriodId == reportPeriodId)
                .GroupBy(i => i.SiteId)
                .Select(g => new { SiteId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.SiteId, x => x.Count);

            var costSavingCounts = await _db.CostSavingInitiatives
                .Where(c => c.ReportPeriodId == reportPeriodId)
                .GroupBy(c => c.SiteId)
                .Select(g => new { SiteId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.SiteId, x => x.Count);

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
                    TrainingCount = trainingCounts.GetValueOrDefault(s.Id),
                    InitiativeCount = initiativeCounts.GetValueOrDefault(s.Id),
                    CostSavingCount = costSavingCounts.GetValueOrDefault(s.Id),
                    ScorecardMetricCount = scorecardCounts.GetValueOrDefault(s.Id)
                };
            }).ToList();
        }

        // ---- Delete a single row (used when correcting a mistaken entry) ----
        // Workflow: update a saved initiative in place (status progress, corrections).
        public async Task<Initiative> UpdateInitiativeAsync(int id, InitiativeUpdateDto dto)
        {
            var entity = await _db.Initiatives.FindAsync(id)
                ?? throw new KeyNotFoundException();
            await EnsureSiteEditableAsync(entity.SiteId, entity.ReportPeriodId);

            if (!Enum.TryParse<CompletionStatus>(dto.Status, ignoreCase: true, out var status))
                throw new InvalidOperationException($"Invalid status '{dto.Status}'.");
            if (string.IsNullOrWhiteSpace(dto.Name) || string.IsNullOrWhiteSpace(dto.Department))
                throw new InvalidOperationException("Name and Department are required.");

            entity.Name = dto.Name.Trim();
            entity.Department = dto.Department.Trim();
            entity.Category = string.IsNullOrWhiteSpace(dto.Category) ? null : dto.Category.Trim();
            entity.FacilitatorName = dto.FacilitatorName?.Trim() ?? "";
            entity.DepartmentHead = dto.DepartmentHead?.Trim() ?? "";
            entity.Status = status;
            entity.Remarks = string.IsNullOrWhiteSpace(dto.Remarks) ? null : dto.Remarks.Trim();

            await _db.SaveChangesAsync();
            return entity;
        }

        // ==================== Initiative change requests ====================
        // Once a month is submitted/approved, initiatives are frozen; corrections
        // go through a change request that corporate approves or rejects.

        private static string SerializeEditable(Initiative i) =>
            System.Text.Json.JsonSerializer.Serialize(new
            {
                name = i.Name,
                department = i.Department,
                category = i.Category,
                facilitatorName = i.FacilitatorName,
                departmentHead = i.DepartmentHead,
                status = i.Status.ToString(),
                remarks = i.Remarks
            });

        public async Task<InitiativeChangeRequest> CreateChangeRequestAsync(
            int initiativeId, ChangeRequestCreateDto dto, string requestedBy)
        {
            var initiative = await _db.Initiatives.FindAsync(initiativeId)
                ?? throw new KeyNotFoundException();

            var period = await _db.ReportPeriods.FindAsync(initiative.ReportPeriodId);
            if (period?.Status == ReportPeriodStatus.Locked)
                throw new InvalidOperationException("This report period is locked — no further changes are possible.");

            if (string.IsNullOrWhiteSpace(dto.Justification))
                throw new InvalidOperationException("A justification is required for a change request.");

            if (!Enum.TryParse<ChangeRequestType>(dto.RequestType, ignoreCase: true, out var type))
                throw new InvalidOperationException($"Invalid request type '{dto.RequestType}'.");

            var hasPending = await _db.InitiativeChangeRequests
                .AnyAsync(cr => cr.InitiativeId == initiativeId && cr.Status == ChangeRequestStatus.Pending);
            if (hasPending)
                throw new InvalidOperationException("This initiative already has a pending change request. Wait for corporate to decide it first.");

            string proposedJson = "{}";
            if (type == ChangeRequestType.Update)
            {
                if (dto.Proposed == null)
                    throw new InvalidOperationException("Proposed values are required for an update request.");
                if (string.IsNullOrWhiteSpace(dto.Proposed.Name) || string.IsNullOrWhiteSpace(dto.Proposed.Department))
                    throw new InvalidOperationException("Name and Department are required.");
                if (!Enum.TryParse<CompletionStatus>(dto.Proposed.Status, ignoreCase: true, out _))
                    throw new InvalidOperationException($"Invalid status '{dto.Proposed.Status}'.");
                proposedJson = System.Text.Json.JsonSerializer.Serialize(new
                {
                    name = dto.Proposed.Name.Trim(),
                    department = dto.Proposed.Department.Trim(),
                    category = string.IsNullOrWhiteSpace(dto.Proposed.Category) ? null : dto.Proposed.Category.Trim(),
                    facilitatorName = dto.Proposed.FacilitatorName?.Trim() ?? "",
                    departmentHead = dto.Proposed.DepartmentHead?.Trim() ?? "",
                    status = dto.Proposed.Status,
                    remarks = string.IsNullOrWhiteSpace(dto.Proposed.Remarks) ? null : dto.Proposed.Remarks.Trim()
                });
            }

            var cr = new InitiativeChangeRequest
            {
                InitiativeId = initiativeId,
                RequestType = type,
                OriginalJson = SerializeEditable(initiative),
                ProposedJson = proposedJson,
                Justification = dto.Justification.Trim(),
                Status = ChangeRequestStatus.Pending,
                RequestedBy = requestedBy,
                RequestedAtUtc = DateTime.UtcNow
            };
            _db.InitiativeChangeRequests.Add(cr);
            await _db.SaveChangesAsync();
            return cr;
        }

        public async Task<InitiativeChangeRequest> DecideChangeRequestAsync(
            int crId, ChangeRequestDecisionDto dto, string decidedBy)
        {
            var cr = await _db.InitiativeChangeRequests
                .Include(c => c.Initiative)
                .FirstOrDefaultAsync(c => c.Id == crId)
                ?? throw new KeyNotFoundException();

            if (cr.Status != ChangeRequestStatus.Pending)
                throw new InvalidOperationException("This change request has already been decided.");

            var period = await _db.ReportPeriods.FindAsync(cr.Initiative.ReportPeriodId);
            if (period?.Status == ReportPeriodStatus.Locked)
                throw new InvalidOperationException("This report period is locked — the change request can no longer be applied.");

            var decision = dto.Decision?.Trim().ToLowerInvariant();
            if (decision == "reject")
            {
                if (string.IsNullOrWhiteSpace(dto.Comments))
                    throw new InvalidOperationException("Comments are required when rejecting a change request.");
                cr.Status = ChangeRequestStatus.Rejected;
            }
            else if (decision == "approve")
            {
                if (cr.RequestType == ChangeRequestType.Delete)
                {
                    _db.Initiatives.Remove(cr.Initiative); // attachments cascade
                }
                else
                {
                    var p = System.Text.Json.JsonSerializer.Deserialize<InitiativeUpdateDto>(
                        cr.ProposedJson,
                        new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                        ?? throw new InvalidOperationException("Stored proposal could not be read.");
                    Enum.TryParse<CompletionStatus>(p.Status, ignoreCase: true, out var status);
                    cr.Initiative.Name = p.Name;
                    cr.Initiative.Department = p.Department;
                    cr.Initiative.Category = p.Category;
                    cr.Initiative.FacilitatorName = p.FacilitatorName;
                    cr.Initiative.DepartmentHead = p.DepartmentHead;
                    cr.Initiative.Status = status;
                    cr.Initiative.Remarks = p.Remarks;
                }
                cr.Status = ChangeRequestStatus.Approved;
            }
            else
            {
                throw new InvalidOperationException($"Unknown decision '{dto.Decision}'. Use 'Approve' or 'Reject'.");
            }

            cr.DecidedBy = decidedBy;
            cr.DecidedAtUtc = DateTime.UtcNow;
            cr.DecisionComments = string.IsNullOrWhiteSpace(dto.Comments) ? null : dto.Comments.Trim();
            await _db.SaveChangesAsync();
            return cr;
        }

        public async Task DeleteInitiativeAsync(int id)
        {
            var entity = await _db.Initiatives.FindAsync(id);
            if (entity != null)
            {
                await EnsureSiteEditableAsync(entity.SiteId, entity.ReportPeriodId);
                _db.Initiatives.Remove(entity);
                await _db.SaveChangesAsync();
            }
        }

        public async Task DeleteTrainingAsync(int id)
        {
            var entity = await _db.TrainingRecords.FindAsync(id);
            if (entity != null)
            {
                await EnsureSiteEditableAsync(entity.SiteId, entity.ReportPeriodId);
                _db.TrainingRecords.Remove(entity);
                await _db.SaveChangesAsync();
            }
        }

        public async Task DeleteCostSavingAsync(int id)
        {
            var entity = await _db.CostSavingInitiatives.FindAsync(id);
            if (entity != null)
            {
                await EnsureSiteEditableAsync(entity.SiteId, entity.ReportPeriodId);
                _db.CostSavingInitiatives.Remove(entity);
                await _db.SaveChangesAsync();
            }
        }
    }
}
