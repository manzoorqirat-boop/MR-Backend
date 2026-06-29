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

            var entities = new List<Initiative>();
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

                entities.Add(new Initiative
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

            if (entities.Count > 0)
            {
                _db.Initiatives.AddRange(entities);
                await _db.SaveChangesAsync();
            }
            result.RowsAccepted = entities.Count;
            return result;
        }

        // ---- Training (sheet 1) ----
        public async Task<ImportResultDto> SaveTrainingAsync(TrainingBulkCreateDto request)
        {
            await EnsurePeriodIsOpenAsync(request.ReportPeriodId);

            var result = new ImportResultDto();
            var entities = new List<TrainingRecord>();

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

                entities.Add(new TrainingRecord
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

            if (entities.Count > 0)
            {
                _db.TrainingRecords.AddRange(entities);
                await _db.SaveChangesAsync();
            }
            result.RowsAccepted = entities.Count;
            return result;
        }

        // ---- Cost Savings (sheet 7) ----
        public async Task<ImportResultDto> SaveCostSavingsAsync(CostSavingBulkCreateDto request)
        {
            await EnsurePeriodIsOpenAsync(request.ReportPeriodId);

            var result = new ImportResultDto();
            var entities = new List<CostSavingInitiative>();

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

                entities.Add(new CostSavingInitiative
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

            if (entities.Count > 0)
            {
                _db.CostSavingInitiatives.AddRange(entities);
                await _db.SaveChangesAsync();
            }
            result.RowsAccepted = entities.Count;
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
                _db.Initiatives.Remove(entity);
                await _db.SaveChangesAsync();
            }
        }

        public async Task DeleteTrainingAsync(int id)
        {
            var entity = await _db.TrainingRecords.FindAsync(id);
            if (entity != null)
            {
                _db.TrainingRecords.Remove(entity);
                await _db.SaveChangesAsync();
            }
        }

        public async Task DeleteCostSavingAsync(int id)
        {
            var entity = await _db.CostSavingInitiatives.FindAsync(id);
            if (entity != null)
            {
                _db.CostSavingInitiatives.Remove(entity);
                await _db.SaveChangesAsync();
            }
        }
    }
}
