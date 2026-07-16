using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using SiteReportApp.Data;
using SiteReportApp.Models;

namespace SiteReportApp.Services
{
    public class MasterImportResult
    {
        public int EquipmentAdded { get; set; }
        public int EquipmentSkipped { get; set; }
        public int DepartmentsAdded { get; set; }
        public int DepartmentsSkipped { get; set; }
        public int CategoriesAdded { get; set; }
        public int CategoriesUpdated { get; set; }
        public int CategoriesSkipped { get; set; }
        public List<string> Errors { get; set; } = new();
    }

    // Standard Excel template for bulk master-data setup: one workbook with
    // three data sheets (Equipment, Departments, System Categories) plus a
    // read-only Locations reference sheet listing the valid location codes.
    // Import is tolerant: valid rows are applied, problem rows are reported
    // per row without failing the whole file. Matching is case-insensitive
    // and by header label, so column order doesn't matter.
    public class MasterDataExcelService
    {
        private readonly AppDbContext _db;
        public MasterDataExcelService(AppDbContext db) { _db = db; }

        public const string EquipmentSheet = "Equipment";
        public const string DepartmentSheet = "Departments";
        public const string CategorySheet = "System Categories";

        // ---- Template ----
        public async Task<byte[]> BuildTemplateAsync()
        {
            var sites = await _db.Sites.Where(s => s.IsActive).OrderBy(s => s.Name).ToListAsync();

            using var wb = new XLWorkbook();

            // Sheet 1: Equipment (per location)
            var eq = wb.Worksheets.Add(EquipmentSheet);
            WriteHeader(eq, "Location Code", "Equipment ID", "Equipment Name");
            eq.Cell(2, 1).Value = sites.FirstOrDefault()?.Code ?? "SITE01";
            eq.Cell(2, 2).Value = "EQP-1001";
            eq.Cell(2, 3).Value = "HPLC-01 (Waters Alliance)";
            eq.Range(2, 1, 2, 3).Style.Font.Italic = true;
            eq.Range(2, 1, 2, 3).Style.Font.FontColor = XLColor.Gray;
            eq.Cell(1, 5).Value = "Example row — replace it. Location Code must match the Locations sheet.";
            eq.Cell(1, 5).Style.Font.FontColor = XLColor.Gray;
            eq.Columns(1, 3).Width = 26;

            // Sheet 2: Departments (global)
            var dep = wb.Worksheets.Add(DepartmentSheet);
            WriteHeader(dep, "Department / Area");
            dep.Cell(2, 1).Value = "Microbiology";
            dep.Cell(2, 1).Style.Font.Italic = true;
            dep.Cell(2, 1).Style.Font.FontColor = XLColor.Gray;
            dep.Column(1).Width = 32;

            // Sheet 3: System Categories (global, with frequency)
            var cat = wb.Worksheets.Add(CategorySheet);
            WriteHeader(cat, "System Category", "Review Frequency (Years)");
            cat.Cell(2, 1).Value = "Critical / Category 5";
            cat.Cell(2, 2).Value = 1;
            cat.Range(2, 1, 2, 2).Style.Font.Italic = true;
            cat.Range(2, 1, 2, 2).Style.Font.FontColor = XLColor.Gray;
            cat.Columns(1, 2).Width = 28;

            // Sheet 4: Locations reference (read-only helper)
            var loc = wb.Worksheets.Add("Locations (reference)");
            WriteHeader(loc, "Location Code", "Location Name");
            var r = 2;
            foreach (var s in sites)
            {
                loc.Cell(r, 1).Value = s.Code;
                loc.Cell(r, 2).Value = s.Name;
                r++;
            }
            loc.Columns(1, 2).Width = 26;

            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return ms.ToArray();
        }

        private static void WriteHeader(IXLWorksheet ws, params string[] headers)
        {
            for (var c = 0; c < headers.Length; c++)
            {
                ws.Cell(1, c + 1).Value = headers[c];
                ws.Cell(1, c + 1).Style.Font.Bold = true;
                ws.Cell(1, c + 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#EEF2FF");
            }
            ws.SheetView.FreezeRows(1);
        }

        // ---- Import ----
        public async Task<MasterImportResult> ImportAsync(Stream file)
        {
            var result = new MasterImportResult();
            using var wb = new XLWorkbook(file);

            var sitesByCode = await _db.Sites
                .ToDictionaryAsync(s => s.Code.Trim(), s => s.Id, StringComparer.OrdinalIgnoreCase);

            // ---------- Equipment ----------
            if (TryGetSheet(wb, EquipmentSheet, out var eq))
            {
                var cols = HeaderMap(eq);
                if (!cols.TryGetValue("location code", out var cLoc) ||
                    !cols.TryGetValue("equipment id", out var cId) ||
                    !cols.TryGetValue("equipment name", out var cName))
                {
                    result.Errors.Add($"Sheet '{EquipmentSheet}': expected headers 'Location Code', 'Equipment ID', 'Equipment Name'.");
                }
                else
                {
                    var existing = await _db.Equipments
                        .Select(e => new { e.SiteId, e.Code })
                        .ToListAsync();
                    var known = new HashSet<string>(existing.Select(e => $"{e.SiteId}|{e.Code}"), StringComparer.OrdinalIgnoreCase);

                    foreach (var row in DataRows(eq))
                    {
                        var loc = Cell(row, cLoc);
                        var code = Cell(row, cId);
                        var name = Cell(row, cName);
                        if (loc == "" && code == "" && name == "") continue;

                        if (loc == "" || code == "" || name == "")
                        { result.Errors.Add($"{EquipmentSheet} row {row.RowNumber()}: all three columns are required."); continue; }
                        if (!sitesByCode.TryGetValue(loc, out var siteId))
                        { result.Errors.Add($"{EquipmentSheet} row {row.RowNumber()}: unknown location code '{loc}' (see the Locations sheet)."); continue; }

                        var key = $"{siteId}|{code}";
                        if (known.Contains(key)) { result.EquipmentSkipped++; continue; }

                        _db.Equipments.Add(new Equipment { SiteId = siteId, Code = code, Name = name, IsActive = true });
                        known.Add(key);
                        result.EquipmentAdded++;
                    }
                }
            }

            // ---------- Departments ----------
            if (TryGetSheet(wb, DepartmentSheet, out var dep))
            {
                var existing = await ActiveListValuesAsync(MasterListKeys.Department);
                var order = await NextSortOrderAsync(MasterListKeys.Department);
                foreach (var row in DataRows(dep))
                {
                    var value = Cell(row, 1);
                    if (value == "") continue;
                    if (existing.Contains(value)) { result.DepartmentsSkipped++; continue; }
                    _db.MasterListItems.Add(new MasterListItem
                    { ListKey = MasterListKeys.Department, Value = value, IsActive = true, SortOrder = order++ });
                    existing.Add(value);
                    result.DepartmentsAdded++;
                }
            }

            // ---------- System Categories ----------
            if (TryGetSheet(wb, CategorySheet, out var cat))
            {
                var cols = HeaderMap(cat);
                cols.TryGetValue("system category", out var cVal);
                cols.TryGetValue("review frequency (years)", out var cFreq);
                if (cVal == 0) cVal = 1;   // tolerate a bare single-column sheet

                var items = await _db.MasterListItems
                    .Where(i => i.ListKey == MasterListKeys.SystemCategory)
                    .ToListAsync();
                var byValue = items.ToDictionary(i => i.Value, StringComparer.OrdinalIgnoreCase);
                var order = items.Count == 0 ? 1 : items.Max(i => i.SortOrder) + 1;

                foreach (var row in DataRows(cat))
                {
                    var value = Cell(row, cVal);
                    if (value == "") continue;
                    int? freq = null;
                    if (cFreq > 0)
                    {
                        var raw = Cell(row, cFreq);
                        if (raw != "")
                        {
                            if (!int.TryParse(raw, out var f) || f < 1 || f > 20)
                            { result.Errors.Add($"{CategorySheet} row {row.RowNumber()}: frequency '{raw}' must be a whole number 1–20."); continue; }
                            freq = f;
                        }
                    }

                    if (byValue.TryGetValue(value, out var existingItem))
                    {
                        if (freq != null && existingItem.FrequencyYears != freq)
                        { existingItem.FrequencyYears = freq; result.CategoriesUpdated++; }
                        else result.CategoriesSkipped++;
                        continue;
                    }

                    var item = new MasterListItem
                    { ListKey = MasterListKeys.SystemCategory, Value = value, IsActive = true, SortOrder = order++, FrequencyYears = freq };
                    _db.MasterListItems.Add(item);
                    byValue[value] = item;
                    result.CategoriesAdded++;
                }
            }

            await _db.SaveChangesAsync();
            return result;
        }

        // ---- helpers ----
        private static bool TryGetSheet(XLWorkbook wb, string name, out IXLWorksheet ws)
        {
            ws = wb.Worksheets.FirstOrDefault(w => string.Equals(w.Name.Trim(), name, StringComparison.OrdinalIgnoreCase))!;
            return ws != null;
        }

        private static Dictionary<string, int> HeaderMap(IXLWorksheet ws)
        {
            var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var header = ws.Row(1);
            foreach (var cell in header.CellsUsed())
                map[cell.GetString().Trim().ToLowerInvariant()] = cell.Address.ColumnNumber;
            return map;
        }

        private static IEnumerable<IXLRow> DataRows(IXLWorksheet ws) =>
            ws.RowsUsed().Where(r => r.RowNumber() > 1);

        private static string Cell(IXLRow row, int col) =>
            col <= 0 ? "" : row.Cell(col).GetString().Trim();

        private async Task<HashSet<string>> ActiveListValuesAsync(string key)
        {
            var values = await _db.MasterListItems
                .Where(i => i.ListKey == key)
                .Select(i => i.Value)
                .ToListAsync();
            return new HashSet<string>(values, StringComparer.OrdinalIgnoreCase);
        }

        private async Task<int> NextSortOrderAsync(string key)
        {
            var max = await _db.MasterListItems.Where(i => i.ListKey == key)
                .Select(i => (int?)i.SortOrder).MaxAsync() ?? 0;
            return max + 1;
        }
    }
}
