using System.Globalization;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using SiteReportApp.Data;
using SiteReportApp.Models;

namespace SiteReportApp.Services
{
    public class QaItImportRow
    {
        public string EquipmentName { get; set; } = "";
        public string EquipmentCode { get; set; } = "";
        public string SoftwareNameVersion { get; set; } = "";
        public string DepartmentArea { get; set; } = "";
        public string SystemCategory { get; set; } = "";
        public string InitialQualificationDate { get; set; } = "";
        public string LastPeriodicReviewDate { get; set; } = "";
        public string NextPlannedDue { get; set; } = "";
        public string DueJustification { get; set; } = "";
        public string ActualDoneOn { get; set; } = "";
        public string ActualDoneBy { get; set; } = "";
    }

    public class QaItImportResult
    {
        public List<QaItImportRow> Rows { get; set; } = new();
        public int EquipmentAddedToMaster { get; set; }
        public int DepartmentsAddedToMaster { get; set; }
        public int CategoriesAddedToMaster { get; set; }
        public List<string> Errors { get; set; } = new();
    }

    // Excel template + import for the QA-IT periodic review register.
    // Import side effect (by design): every equipment ID, department, and
    // system category found in the file is upserted into MASTER DATA with
    // case-insensitive dedup — nothing is ever added twice. The parsed rows
    // are returned to the register grid for review; the register itself is
    // saved by the user afterwards (so the frequency/justification rule
    // still applies at save time).
    public class QaItExcelService
    {
        private readonly AppDbContext _db;
        public QaItExcelService(AppDbContext db) { _db = db; }

        public const string SheetName = "Periodic Reviews";

        // ---- Template (with reference sheets for the chosen location) ----
        public async Task<byte[]> BuildTemplateAsync(int siteId)
        {
            var site = await _db.Sites.FindAsync(siteId);
            var equipment = await _db.Equipments
                .Where(e => e.SiteId == siteId && e.IsActive).OrderBy(e => e.Code).ToListAsync();
            var departments = await _db.MasterListItems
                .Where(i => i.ListKey == MasterListKeys.Department && i.IsActive)
                .OrderBy(i => i.SortOrder).ToListAsync();
            var categories = await _db.MasterListItems
                .Where(i => i.ListKey == MasterListKeys.SystemCategory && i.IsActive)
                .OrderBy(i => i.SortOrder).ToListAsync();

            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add(SheetName);
            var headers = new[]
            {
                "Equipment ID", "Equipment Name", "Software Name & Version", "Department / Area",
                "System Category", "Initial Qualification Date (DD-MM-YYYY)",
                "Last Periodic Review Date (DD-MM-YYYY)", "Next Planned Review Due (MMM/YYYY)",
                "Actual Review Done On (MMM/YYYY)", "Done By", "Due Justification (only if planned differs from frequency rule)"
            };
            for (var c = 0; c < headers.Length; c++)
            {
                ws.Cell(1, c + 1).Value = headers[c];
                ws.Cell(1, c + 1).Style.Font.Bold = true;
                ws.Cell(1, c + 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#EEF2FF");
            }
            ws.SheetView.FreezeRows(1);
            ws.Cell(2, 1).Value = equipment.FirstOrDefault()?.Code ?? "EQP-1001";
            ws.Cell(2, 2).Value = equipment.FirstOrDefault()?.Name ?? "HPLC-01 (Waters)";
            ws.Cell(2, 3).Value = "Empower 3 FR5";
            ws.Cell(2, 4).Value = departments.FirstOrDefault()?.Value ?? "QC";
            ws.Cell(2, 5).Value = categories.FirstOrDefault()?.Value ?? "Major / Category 4";
            ws.Cell(2, 6).Value = "15-01-2025";
            ws.Cell(2, 7).Value = "15-01-2026";
            ws.Cell(2, 8).Value = "JAN/2028";
            ws.Range(2, 1, 2, 11).Style.Font.Italic = true;
            ws.Range(2, 1, 2, 11).Style.Font.FontColor = XLColor.Gray;
            ws.Columns(1, 11).Width = 26;

            // Reference sheets: what already exists (new values in the file are
            // added to master data automatically on import).
            var refEq = wb.Worksheets.Add($"Equipment @ {site?.Code ?? "site"}");
            refEq.Cell(1, 1).Value = "Equipment ID"; refEq.Cell(1, 2).Value = "Equipment Name";
            refEq.Row(1).Style.Font.Bold = true;
            var r = 2;
            foreach (var e in equipment) { refEq.Cell(r, 1).Value = e.Code; refEq.Cell(r, 2).Value = e.Name; r++; }
            refEq.Columns(1, 2).Width = 26;

            var refLists = wb.Worksheets.Add("Departments & Categories");
            refLists.Cell(1, 1).Value = "Department / Area";
            refLists.Cell(1, 3).Value = "System Category";
            refLists.Cell(1, 4).Value = "Frequency (yrs)";
            refLists.Row(1).Style.Font.Bold = true;
            r = 2;
            foreach (var d in departments) { refLists.Cell(r, 1).Value = d.Value; r++; }
            r = 2;
            foreach (var c in categories)
            {
                refLists.Cell(r, 3).Value = c.Value;
                if (c.FrequencyYears != null) refLists.Cell(r, 4).Value = c.FrequencyYears.Value;
                r++;
            }
            refLists.Columns(1, 4).Width = 26;

            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return ms.ToArray();
        }

        // ---- Import: parse rows + upsert masters (deduped) ----
        public async Task<QaItImportResult> ImportAsync(int siteId, Stream file)
        {
            var result = new QaItImportResult();
            using var wb = new XLWorkbook(file);
            var ws = wb.Worksheets.FirstOrDefault(w =>
                    string.Equals(w.Name.Trim(), SheetName, StringComparison.OrdinalIgnoreCase))
                ?? wb.Worksheets.First();

            var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var cell in ws.Row(1).CellsUsed())
            {
                var label = cell.GetString().Trim().ToLowerInvariant();
                // header labels carry format hints in parentheses — match on the stem
                var stem = label.Split('(')[0].Trim();
                map[stem] = cell.Address.ColumnNumber;
            }
            int Col(string stem) => map.TryGetValue(stem, out var c) ? c : 0;

            var cCode = Col("equipment id");
            var cName = Col("equipment name");
            if (cCode == 0 || cName == 0)
            {
                result.Errors.Add("Headers 'Equipment ID' and 'Equipment Name' are required — download the template for the expected layout.");
                return result;
            }
            var cSoft = Col("software name & version");
            var cDept = Col("department / area");
            var cCat = Col("system category");
            var cIq = Col("initial qualification date");
            var cLast = Col("last periodic review date");
            var cDue = Col("next planned review due");
            var cDone = Col("actual review done on");
            var cBy = Col("done by");
            var cJust = Col("due justification");

            // ---- Master snapshots for dedup (case-insensitive) ----
            var equipment = await _db.Equipments.Where(e => e.SiteId == siteId).ToListAsync();
            var eqByCode = new Dictionary<string, Equipment>(StringComparer.OrdinalIgnoreCase);
            foreach (var e in equipment) eqByCode[e.Code] = e;

            var deptItems = await _db.MasterListItems
                .Where(i => i.ListKey == MasterListKeys.Department).ToListAsync();
            var deptSet = new HashSet<string>(deptItems.Select(d => d.Value), StringComparer.OrdinalIgnoreCase);
            var deptCanonical = deptItems.ToDictionary(d => d.Value, d => d.Value, StringComparer.OrdinalIgnoreCase);
            var deptOrder = deptItems.Count == 0 ? 1 : deptItems.Max(i => i.SortOrder) + 1;

            var catItems = await _db.MasterListItems
                .Where(i => i.ListKey == MasterListKeys.SystemCategory).ToListAsync();
            var catSet = new HashSet<string>(catItems.Select(c => c.Value), StringComparer.OrdinalIgnoreCase);
            var catCanonical = catItems.ToDictionary(c => c.Value, c => c.Value, StringComparer.OrdinalIgnoreCase);
            var catOrder = catItems.Count == 0 ? 1 : catItems.Max(i => i.SortOrder) + 1;

            var seenCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var row in ws.RowsUsed().Where(x => x.RowNumber() > 1))
            {
                var code = Text(row, cCode);
                var name = Text(row, cName);
                if (code == "" && name == "") continue;
                var rn = row.RowNumber();

                if (code == "" || name == "")
                { result.Errors.Add($"Row {rn}: Equipment ID and Equipment Name are both required."); continue; }
                if (!seenCodes.Add(code))
                { result.Errors.Add($"Row {rn}: duplicate Equipment ID '{code}' inside the file — only the first occurrence was used."); continue; }

                // ---- Equipment master upsert (no repeats) ----
                if (eqByCode.TryGetValue(code, out var existingEq))
                {
                    name = existingEq.Name;   // master is authoritative for the name
                }
                else
                {
                    var eq = new Equipment { SiteId = siteId, Code = code, Name = name, IsActive = true };
                    _db.Equipments.Add(eq);
                    eqByCode[code] = eq;
                    result.EquipmentAddedToMaster++;
                }

                // ---- Department master upsert ----
                var dept = Text(row, cDept);
                if (dept != "")
                {
                    if (deptSet.Contains(dept)) dept = deptCanonical[dept];
                    else
                    {
                        _db.MasterListItems.Add(new MasterListItem
                        { ListKey = MasterListKeys.Department, Value = dept, IsActive = true, SortOrder = deptOrder++ });
                        deptSet.Add(dept);
                        deptCanonical[dept] = dept;
                        result.DepartmentsAddedToMaster++;
                    }
                }

                // ---- System category master upsert (frequency unknown -> set later) ----
                var cat = Text(row, cCat);
                if (cat != "")
                {
                    if (catSet.Contains(cat)) cat = catCanonical[cat];
                    else
                    {
                        _db.MasterListItems.Add(new MasterListItem
                        { ListKey = MasterListKeys.SystemCategory, Value = cat, IsActive = true, SortOrder = catOrder++ });
                        catSet.Add(cat);
                        catCanonical[cat] = cat;
                        result.CategoriesAddedToMaster++;
                    }
                }

                // ---- Dates & months ----
                var iq = ParseDate(row, cIq, rn, "Initial Qualification Date", result.Errors);
                var last = ParseDate(row, cLast, rn, "Last Periodic Review Date", result.Errors);
                var due = ParseMonth(row, cDue, rn, "Next Planned Review Due", result.Errors);
                var done = ParseMonth(row, cDone, rn, "Actual Review Done On", result.Errors);

                result.Rows.Add(new QaItImportRow
                {
                    EquipmentCode = code,
                    EquipmentName = name,
                    SoftwareNameVersion = Text(row, cSoft),
                    DepartmentArea = dept,
                    SystemCategory = cat,
                    InitialQualificationDate = iq,
                    LastPeriodicReviewDate = last,
                    NextPlannedDue = due,
                    ActualDoneOn = done,
                    ActualDoneBy = Text(row, cBy),
                    DueJustification = Text(row, cJust)
                });
            }

            await _db.SaveChangesAsync();   // masters only; register rows go back for review
            return result;
        }

        private static string Text(IXLRow row, int col) =>
            col <= 0 ? "" : row.Cell(col).GetString().Trim();

        // Full date -> "yyyy-MM-dd". Accepts Excel dates or dd-MM-yyyy / dd/MM/yyyy / yyyy-MM-dd text.
        private static string ParseDate(IXLRow row, int col, int rn, string label, List<string> errors)
        {
            if (col <= 0) return "";
            var cell = row.Cell(col);
            if (cell.IsEmpty()) return "";
            if (cell.DataType == XLDataType.DateTime)
                return cell.GetDateTime().ToString("yyyy-MM-dd");
            var raw = cell.GetString().Trim();
            if (raw == "") return "";
            string[] formats = { "dd-MM-yyyy", "dd/MM/yyyy", "yyyy-MM-dd", "d-M-yyyy", "d/M/yyyy" };
            if (DateTime.TryParseExact(raw, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
                return d.ToString("yyyy-MM-dd");
            errors.Add($"Row {rn}: {label} '{raw}' not understood — use DD-MM-YYYY. Value ignored.");
            return "";
        }

        // Month -> "yyyy-MM". Accepts Excel dates, "MMM/yyyy" (JAN/2028), "MMM-yyyy", "yyyy-MM", "MM/yyyy".
        private static string ParseMonth(IXLRow row, int col, int rn, string label, List<string> errors)
        {
            if (col <= 0) return "";
            var cell = row.Cell(col);
            if (cell.IsEmpty()) return "";
            if (cell.DataType == XLDataType.DateTime)
                return cell.GetDateTime().ToString("yyyy-MM");
            var raw = cell.GetString().Trim();
            if (raw == "") return "";
            string[] formats = { "MMM/yyyy", "MMM-yyyy", "MMMM/yyyy", "yyyy-MM", "MM/yyyy", "M/yyyy" };
            if (DateTime.TryParseExact(raw, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
                return d.ToString("yyyy-MM");
            errors.Add($"Row {rn}: {label} '{raw}' not understood — use MMM/YYYY (e.g. JAN/2028). Value ignored.");
            return "";
        }
    }
}
