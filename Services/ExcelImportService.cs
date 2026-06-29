using ClosedXML.Excel;
using SiteReportApp.Dtos;
using SiteReportApp.Models;

namespace SiteReportApp.Services
{
    // Parses an uploaded .xlsx matching the site's standard 7-sheet template
    // and converts each sheet's rows into the bulk-create DTOs that
    // DataEntryService already knows how to save.
    //
    // Expected sheet names (case-insensitive, partial match):
    //   "Training", "Documentation Simplification", "Regulatory Compliance",
    //   "Productivity Enhancement", "Lean Laboratory", "Digitalization", "Cost Savings"
    //
    // Expected header row = row 1 on each sheet, data starts row 2.
    public class ExcelImportService
    {
        private static readonly Dictionary<string, InitiativeType> InitiativeSheetMap = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Documentation Simplification"] = InitiativeType.DocumentationSimplification,
            ["Regulatory Compliance"] = InitiativeType.RegulatoryCompliance,
            ["Productivity Enhancement"] = InitiativeType.ProductivityEnhancement,
            ["Lean Laboratory"] = InitiativeType.LeanLaboratory,
            ["Digitalization"] = InitiativeType.Digitalization,
        };

        public class ParsedImport
        {
            public TrainingBulkCreateDto? Training { get; set; }
            public List<InitiativeBulkCreateDto> Initiatives { get; set; } = new();
            public CostSavingBulkCreateDto? CostSavings { get; set; }
            public List<string> Warnings { get; set; } = new();
        }

        public ParsedImport ParseWorkbook(Stream fileStream, int siteId, int reportPeriodId)
        {
            var result = new ParsedImport();
            using var workbook = new XLWorkbook(fileStream);

            foreach (var worksheet in workbook.Worksheets)
            {
                var sheetName = worksheet.Name.Trim();

                if (sheetName.Contains("Training", StringComparison.OrdinalIgnoreCase))
                {
                    result.Training = ParseTrainingSheet(worksheet, siteId, reportPeriodId, result.Warnings);
                    continue;
                }

                if (sheetName.Contains("Cost Saving", StringComparison.OrdinalIgnoreCase))
                {
                    result.CostSavings = ParseCostSavingSheet(worksheet, siteId, reportPeriodId, result.Warnings);
                    continue;
                }

                var matchedType = InitiativeSheetMap
                    .FirstOrDefault(kvp => sheetName.Contains(kvp.Key, StringComparison.OrdinalIgnoreCase));

                if (!matchedType.Equals(default(KeyValuePair<string, InitiativeType>)))
                {
                    var dto = ParseInitiativeSheet(worksheet, siteId, reportPeriodId, matchedType.Value, result.Warnings);
                    if (dto != null) result.Initiatives.Add(dto);
                    continue;
                }

                result.Warnings.Add($"Sheet '{sheetName}' did not match any known template — skipped.");
            }

            return result;
        }

        // Sheet 1: S.no | Topic | Training Imparted by | Months | Department | STATUS
        private TrainingBulkCreateDto ParseTrainingSheet(IXLWorksheet ws, int siteId, int reportPeriodId, List<string> warnings)
        {
            var dto = new TrainingBulkCreateDto { SiteId = siteId, ReportPeriodId = reportPeriodId };
            var rows = ws.RowsUsed().Skip(1); // skip header

            foreach (var row in rows)
            {
                var topic = row.Cell(2).GetString().Trim();
                if (string.IsNullOrWhiteSpace(topic)) continue; // blank row, end of data

                var statusRaw = row.Cell(6).GetString().Trim();
                dto.Rows.Add(new TrainingCreateDto
                {
                    SiteId = siteId,
                    ReportPeriodId = reportPeriodId,
                    SerialNo = (int)row.Cell(1).GetDouble(),
                    Topic = topic,
                    TrainingImpartedBy = row.Cell(3).GetString().Trim(),
                    Department = row.Cell(5).GetString().Trim(),
                    Status = MapTrainingStatus(statusRaw, row.RowNumber(), warnings)
                });
            }
            return dto;
        }

        // Sheets 2-6: S.No | <Topic column> | Departments | Facilitator name | Department Head | Status | Remarks
        // (Lean Laboratory & Digitalization insert "Category" between Departments and Facilitator name)
        private InitiativeBulkCreateDto? ParseInitiativeSheet(
            IXLWorksheet ws, int siteId, int reportPeriodId, InitiativeType type, List<string> warnings)
        {
            bool hasCategory = type is InitiativeType.LeanLaboratory or InitiativeType.Digitalization;
            var dto = new InitiativeBulkCreateDto { SiteId = siteId, ReportPeriodId = reportPeriodId, Type = type.ToString() };
            var rows = ws.RowsUsed().Skip(1);

            foreach (var row in rows)
            {
                var name = row.Cell(2).GetString().Trim();
                if (string.IsNullOrWhiteSpace(name)) continue;

                int col = 3;
                var department = row.Cell(col++).GetString().Trim();
                string? category = null;
                if (hasCategory)
                    category = row.Cell(col++).GetString().Trim();
                var facilitator = row.Cell(col++).GetString().Trim();
                var deptHead = row.Cell(col++).GetString().Trim();
                var statusRaw = row.Cell(col++).GetString().Trim();
                var remarks = row.Cell(col++).GetString().Trim();

                dto.Rows.Add(new InitiativeCreateDto
                {
                    SiteId = siteId,
                    ReportPeriodId = reportPeriodId,
                    Type = type.ToString(),
                    SerialNo = (int)row.Cell(1).GetDouble(),
                    Name = name,
                    Department = department,
                    Category = category,
                    FacilitatorName = facilitator,
                    DepartmentHead = deptHead,
                    Status = MapCompletionStatus(statusRaw, row.RowNumber(), warnings),
                    Remarks = string.IsNullOrWhiteSpace(remarks) ? null : remarks
                });
            }
            return dto.Rows.Count > 0 ? dto : null;
        }

        // Sheet 7: S.No | Name of Projects/Initiatives | Potential Saving (in Lacs) | Project Status | Validated by Finance | Remarks
        private CostSavingBulkCreateDto ParseCostSavingSheet(IXLWorksheet ws, int siteId, int reportPeriodId, List<string> warnings)
        {
            var dto = new CostSavingBulkCreateDto { SiteId = siteId, ReportPeriodId = reportPeriodId };
            var rows = ws.RowsUsed().Skip(1);

            foreach (var row in rows)
            {
                var projectName = row.Cell(2).GetString().Trim();
                if (string.IsNullOrWhiteSpace(projectName)) continue;

                var savingRaw = row.Cell(3).GetString().Trim();
                decimal saving = 0;
                decimal.TryParse(savingRaw, out saving);

                var validatedRaw = row.Cell(5).GetString().Trim();
                bool validated = validatedRaw.Equals("Yes", StringComparison.OrdinalIgnoreCase);

                var statusRaw = row.Cell(4).GetString().Trim();
                var remarks = row.Cell(6).GetString().Trim();

                dto.Rows.Add(new CostSavingCreateDto
                {
                    SiteId = siteId,
                    ReportPeriodId = reportPeriodId,
                    SerialNo = (int)row.Cell(1).GetDouble(),
                    ProjectName = projectName,
                    PotentialSavingLacs = saving,
                    ProjectStatus = MapProjectStatus(statusRaw, row.RowNumber(), warnings),
                    ValidatedByFinance = validated,
                    Remarks = string.IsNullOrWhiteSpace(remarks) ? null : remarks
                });
            }
            return dto;
        }

        // ---- Status text mapping helpers (sheets use free text like "Completed", "In Progress", "Completion") ----
        private string MapCompletionStatus(string raw, int rowNumber, List<string> warnings)
        {
            var normalized = raw.Replace(" ", "").Replace("/", "").ToLowerInvariant();
            if (normalized.Contains("complet")) return nameof(CompletionStatus.Completed);
            if (normalized.Contains("progress")) return nameof(CompletionStatus.InProgress);
            if (normalized.Contains("delay")) return nameof(CompletionStatus.Delayed);
            if (normalized.Contains("notstarted") || string.IsNullOrWhiteSpace(normalized))
                return nameof(CompletionStatus.NotStarted);

            warnings.Add($"Row {rowNumber}: unrecognized status '{raw}' — defaulted to NotStarted");
            return nameof(CompletionStatus.NotStarted);
        }

        private string MapTrainingStatus(string raw, int rowNumber, List<string> warnings)
        {
            var normalized = raw.Trim().ToLowerInvariant();
            if (normalized.Contains("complet")) return nameof(TrainingStatus.Completed);
            if (normalized.Contains("postpon")) return nameof(TrainingStatus.Postponed);
            if (normalized.Contains("plan") || string.IsNullOrWhiteSpace(normalized))
                return nameof(TrainingStatus.Planned);

            warnings.Add($"Row {rowNumber}: unrecognized training status '{raw}' — defaulted to Planned");
            return nameof(TrainingStatus.Planned);
        }

        private string MapProjectStatus(string raw, int rowNumber, List<string> warnings)
        {
            var normalized = raw.Trim().ToLowerInvariant();
            if (normalized.Contains("complet")) return nameof(ProjectStatus.Completed);
            if (normalized.Contains("hold")) return nameof(ProjectStatus.OnHold);
            if (normalized.Contains("progress")) return nameof(ProjectStatus.InProgress);
            if (normalized.Contains("propos") || string.IsNullOrWhiteSpace(normalized))
                return nameof(ProjectStatus.Proposed);

            warnings.Add($"Row {rowNumber}: unrecognized project status '{raw}' — defaulted to Proposed");
            return nameof(ProjectStatus.Proposed);
        }
    }
}
