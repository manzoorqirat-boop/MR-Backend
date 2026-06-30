using System.Globalization;
using ClosedXML.Excel;
using SiteReportApp.Dtos;

namespace SiteReportApp.Services
{
    // Builds the blank upload template (one sheet per metric, matching the standard
    // "Monthly Site Scorecard" tab names) and parses an uploaded workbook back into
    // ScorecardSaveDto rows. Column matching is by header label, so the order of
    // columns in the uploaded file doesn't have to be exact.
    public class ScorecardExcelService
    {
        private readonly ScorecardService _scorecard;
        public ScorecardExcelService(ScorecardService scorecard) { _scorecard = scorecard; }

        // ---- Generate a blank template workbook ----
        // Layout per sheet matches the original: row 1 = category banner,
        // row 2 = headers (Site Name | Months | <input + computed columns>),
        // computed columns are shown but left blank (the app recomputes them).
        public byte[] BuildTemplate()
        {
            using var wb = new XLWorkbook();
            foreach (var metric in ScorecardSchema.Metrics.OrderBy(m => m.Order))
            {
                var name = SafeSheetName(metric.SheetName);
                var ws = wb.Worksheets.Add(name);

                ws.Cell(1, 1).Value = metric.Category;
                ws.Cell(1, 1).Style.Font.Bold = true;

                ws.Cell(2, 1).Value = "Site Name";
                ws.Cell(2, 2).Value = "Months";
                int c = 3;
                foreach (var col in metric.Columns)
                {
                    ws.Cell(2, c).Value = col.Label;
                    if (col.Type == ScColType.Computed)
                        ws.Cell(2, c).Style.Font.FontColor = XLColor.Gray; // hint: auto-calculated
                    c++;
                }
                ws.Row(2).Style.Font.Bold = true;
                ws.SheetView.FreezeRows(2);
                ws.Columns().AdjustToContents();
            }

            // Reference / instructions sheet
            var info = wb.Worksheets.Add("_Instructions");
            info.Cell(1, 1).Value = "Monthly Site Scorecard — Upload Template";
            info.Cell(1, 1).Style.Font.Bold = true;
            info.Cell(3, 1).Value = "1. Fill one sheet per metric. Put the site name in column A and the month in column B (any date in that month).";
            info.Cell(4, 1).Value = "2. Grey columns are auto-calculated by the app — you may leave them blank.";
            info.Cell(5, 1).Value = "3. Multi-row sheets (e.g. Human Error, Audit Performance, Man Power status) may have several rows per site/month.";
            info.Cell(6, 1).Value = "4. Upload via the 'Scorecard Import' page after selecting the site and period.";
            info.Columns().AdjustToContents();

            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return ms.ToArray();
        }

        private static string SafeSheetName(string name)
        {
            // Excel sheet names max 31 chars, no : \ / ? * [ ]
            var clean = new string(name.Where(ch => !"\\/?*[]:".Contains(ch)).ToArray()).Trim();
            return clean.Length > 31 ? clean[..31] : clean;
        }

        // ---- Parse an uploaded workbook into save DTOs ----
        // Each recognised sheet becomes a ScorecardSaveDto for (siteId, reportPeriodId).
        // The siteId/period come from the upload form (same model as the existing
        // ExcelImportController), so the Site Name / Months columns in the file are
        // informational only.
        public (List<ScorecardSaveDto> saves, ScorecardImportResultDto result) ParseWorkbook(
            Stream fileStream, int siteId, int reportPeriodId)
        {
            var result = new ScorecardImportResultDto();
            var saves = new List<ScorecardSaveDto>();
            using var wb = new XLWorkbook(fileStream);

            foreach (var ws in wb.Worksheets)
            {
                if (ws.Name.StartsWith("_")) continue; // skip instructions
                var metric = ScorecardSchema.FindBySheet(ws.Name);
                var sheetResult = new ScorecardImportSheetResultDto { SheetName = ws.Name, Matched = metric != null };
                if (metric == null)
                {
                    result.Warnings.Add($"Sheet '{ws.Name}' did not match any known metric — skipped.");
                    result.Sheets.Add(sheetResult);
                    continue;
                }
                sheetResult.MetricKey = metric.Key;

                // Find header row = the row containing "Site Name" within the first 4 rows.
                int headerRow = 0;
                for (int r = 1; r <= 4; r++)
                {
                    var rowText = string.Join(" ", ws.Row(r).CellsUsed().Select(x => x.GetString()));
                    if (rowText.Contains("Site Name", StringComparison.OrdinalIgnoreCase)) { headerRow = r; break; }
                }
                if (headerRow == 0) headerRow = 2;

                // Map each input column to its Excel column index, by matching the header label.
                var headerCells = ws.Row(headerRow).CellsUsed()
                    .ToDictionary(x => x.Address.ColumnNumber, x => Norm(x.GetString()));
                var colToExcel = new Dictionary<string, int>();
                foreach (var col in metric.Columns.Where(c => c.Type != ScColType.Computed))
                {
                    var match = headerCells.FirstOrDefault(kv => kv.Value == Norm(col.Label));
                    if (match.Key != 0) colToExcel[col.Key] = match.Key;
                }

                var save = new ScorecardSaveDto { SiteId = siteId, ReportPeriodId = reportPeriodId, MetricKey = metric.Key };
                int rowIdx = 0;
                var lastRow = ws.LastRowUsed()?.RowNumber() ?? headerRow;
                for (int r = headerRow + 1; r <= lastRow; r++)
                {
                    var cells = new Dictionary<string, string?>();
                    bool any = false;
                    foreach (var (key, excelCol) in colToExcel)
                    {
                        var cell = ws.Cell(r, excelCol);
                        var val = ReadCell(cell);
                        if (!string.IsNullOrWhiteSpace(val)) { cells[key] = val; any = true; }
                    }
                    if (!any) continue; // blank row
                    save.Rows.Add(new ScorecardRowDto { RowIndex = rowIdx++, Cells = cells });
                    sheetResult.RowsAccepted++;
                }

                if (save.Rows.Count > 0) saves.Add(save);
                result.Sheets.Add(sheetResult);
            }

            return (saves, result);
        }

        private static string Norm(string s) =>
            new string(s.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());

        private static string? ReadCell(IXLCell cell)
        {
            if (cell.IsEmpty()) return null;
            if (cell.DataType == XLDataType.DateTime && cell.TryGetValue<DateTime>(out var dt))
                return dt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            if (cell.TryGetValue<double>(out var n))
                return n.ToString("R", CultureInfo.InvariantCulture);
            return cell.GetString().Trim();
        }
    }
}
