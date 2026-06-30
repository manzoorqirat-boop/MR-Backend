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

        // ---- Initiatives (sheets 2-6) ----
        // Upsert by natural key (SiteId, ReportPeriodId, Type, SerialNo): existing rows are
        // updated in place, new serials are inserted. This makes re-imports and double-submits
        // idempotent instead of creating duplicate rows.
        public async Task<ImportResultDto> SaveInitiativesAsync(InitiativeBulkCreateDto request)
        {
            await EnsurePeriodIsOpenAsync(request.ReportPeriodId);

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
            await EnsurePeriodIsOpenAsync(request.ReportPeriodId);

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
            await EnsurePeriodIsOpenAsync(request.ReportPeriodId);

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

        // ---- Mark a site's monthly submission complete ----
        public async Task<SiteSubmission> MarkSubmittedAsync(SiteSubmissionCreateDto request)
        {
            var existing = await _db.SiteSubmissions
                .FirstOrDefaultAsync(ss => ss.SiteId == request.SiteId && ss.ReportPeriodId == request.ReportPeriodId);

            if (existing != null)
            {
                existing.IsSubmitted = true;
                existing.SubmittedAtUtc = DateTime.UtcNow;
                existing.SubmittedBy = request.SubmittedBy;
            }
            else
            {
                existing = new SiteSubmission
                {
                    SiteId = request.SiteId,
                    ReportPeriodId = request.ReportPeriodId,
                    IsSubmitted = true,
                    SubmittedAtUtc = DateTime.UtcNow,
                    SubmittedBy = request.SubmittedBy
                };
                _db.SiteSubmissions.Add(existing);
            }

            await _db.SaveChangesAsync();
            return existing;
        }

        // ---- Delete a single row (used when correcting a mistaken entry) ----
        public async Task DeleteInitiativeAsync(int id)
        {
            var entity = await _db.Initiatives.FindAsync(id);
            if (entity != null)
            {
                await EnsurePeriodIsOpenAsync(entity.ReportPeriodId);
                _db.Initiatives.Remove(entity);
                await _db.SaveChangesAsync();
            }
        }

        public async Task DeleteTrainingAsync(int id)
        {
            var entity = await _db.TrainingRecords.FindAsync(id);
            if (entity != null)
            {
                await EnsurePeriodIsOpenAsync(entity.ReportPeriodId);
                _db.TrainingRecords.Remove(entity);
                await _db.SaveChangesAsync();
            }
        }

        public async Task DeleteCostSavingAsync(int id)
        {
            var entity = await _db.CostSavingInitiatives.FindAsync(id);
            if (entity != null)
            {
                await EnsurePeriodIsOpenAsync(entity.ReportPeriodId);
                _db.CostSavingInitiatives.Remove(entity);
                await _db.SaveChangesAsync();
            }
        }
    }
}
