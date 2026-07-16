using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SiteReportApp.Auth;
using SiteReportApp.Data;
using SiteReportApp.Models;
using SiteReportApp.Services;

namespace SiteReportApp.Controllers
{
    public class EquipmentCreateDto
    {
        public int SiteId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
    }

    public class ListItemCreateDto
    {
        public string Value { get; set; } = string.Empty;
        public int? FrequencyYears { get; set; }   // systemCategory: review frequency
    }

    public class FrequencyUpdateDto
    {
        public int? FrequencyYears { get; set; }
    }

    // Master data: equipment per site + generic controlled lists.
    // Everyone reads (dropdowns); only corporate manages.
    [ApiController]
    [Route("api/master")]
    [Authorize]
    public class MasterDataController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly MasterDataExcelService _excel;

        public MasterDataController(AppDbContext db, MasterDataExcelService excel)
        {
            _db = db;
            _excel = excel;
        }

        // ==================== Excel template + bulk import ====================

        // GET /api/master/template — the standard workbook: Equipment,
        // Departments, System Categories + a Locations reference sheet.
        [HttpGet("template")]
        [Authorize(Roles = "Corporate")]
        public async Task<IActionResult> DownloadTemplate()
        {
            var bytes = await _excel.BuildTemplateAsync();
            return File(bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "MasterData_Template.xlsx");
        }

        // POST /api/master/import — multipart upload of the filled template.
        // Valid rows are applied; problem rows come back as per-row errors.
        [HttpPost("import")]
        [Authorize(Roles = "Corporate")]
        [RequestSizeLimit(10 * 1024 * 1024)]
        public async Task<IActionResult> Import(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { error = "No file uploaded." });
            var ext = Path.GetExtension(file.FileName);
            if (!string.Equals(ext, ".xlsx", StringComparison.OrdinalIgnoreCase))
                return BadRequest(new { error = "Please upload an .xlsx file (use the downloaded template)." });

            try
            {
                using var stream = file.OpenReadStream();
                var result = await _excel.ImportAsync(stream);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return Conflict(new { error = $"The file could not be read as an Excel workbook: {ex.Message}" });
            }
        }

        // ==================== Equipment / Instrument master ====================

        // GET /api/master/equipment?siteId=1&includeInactive=false
        [HttpGet("equipment")]
        public async Task<IActionResult> GetEquipment([FromQuery] int siteId, [FromQuery] bool includeInactive = false)
        {
            if (!User.CanAccessSite(siteId)) return Forbid();
            var query = _db.Equipments.Where(e => e.SiteId == siteId);
            if (!includeInactive) query = query.Where(e => e.IsActive);
            var items = await query.OrderBy(e => e.Name).ToListAsync();
            return Ok(items.Select(e => new { e.Id, e.SiteId, e.Name, e.Code, e.IsActive }));
        }

        // POST /api/master/equipment — corporate adds to a site's equipment master
        [HttpPost("equipment")]
        [Authorize(Roles = "Corporate")]
        public async Task<IActionResult> CreateEquipment([FromBody] EquipmentCreateDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Name) || string.IsNullOrWhiteSpace(dto.Code))
                return BadRequest(new { error = "Equipment name and ID are both required." });
            if (!await _db.Sites.AnyAsync(s => s.Id == dto.SiteId))
                return BadRequest(new { error = "Site does not exist." });

            var code = dto.Code.Trim();
            if (await _db.Equipments.AnyAsync(e => e.SiteId == dto.SiteId && e.Code == code))
                return Conflict(new { error = $"Equipment ID '{code}' already exists for this site." });

            var eq = new Equipment { SiteId = dto.SiteId, Name = dto.Name.Trim(), Code = code, IsActive = true };
            _db.Equipments.Add(eq);
            await _db.SaveChangesAsync();
            return Ok(new { eq.Id, eq.SiteId, eq.Name, eq.Code, eq.IsActive });
        }

        // PATCH /api/master/equipment/5/toggle-active — retire/restore (no hard delete;
        // registers reference the values historically)
        [HttpPatch("equipment/{id}/toggle-active")]
        [Authorize(Roles = "Corporate")]
        public async Task<IActionResult> ToggleEquipment(int id)
        {
            var eq = await _db.Equipments.FindAsync(id);
            if (eq == null) return NotFound();
            eq.IsActive = !eq.IsActive;
            await _db.SaveChangesAsync();
            return Ok(new { eq.Id, eq.SiteId, eq.Name, eq.Code, eq.IsActive });
        }

        // ==================== Generic controlled lists ====================

        private static bool ValidKey(string key) => MasterListKeys.All.Contains(key);

        // GET /api/master/lists/department  |  /api/master/lists/systemCategory
        [HttpGet("lists/{key}")]
        public async Task<IActionResult> GetList(string key, [FromQuery] bool includeInactive = false)
        {
            if (!ValidKey(key)) return BadRequest(new { error = $"Unknown list '{key}'." });
            var query = _db.MasterListItems.Where(i => i.ListKey == key);
            if (!includeInactive) query = query.Where(i => i.IsActive);
            var items = await query.OrderBy(i => i.SortOrder).ThenBy(i => i.Value).ToListAsync();
            return Ok(items.Select(i => new { i.Id, i.Value, i.IsActive, i.FrequencyYears }));
        }

        // POST /api/master/lists/department  body: { value }
        [HttpPost("lists/{key}")]
        [Authorize(Roles = "Corporate")]
        public async Task<IActionResult> AddListItem(string key, [FromBody] ListItemCreateDto dto)
        {
            if (!ValidKey(key)) return BadRequest(new { error = $"Unknown list '{key}'." });
            var value = dto.Value?.Trim() ?? "";
            if (value.Length == 0) return BadRequest(new { error = "Value is required." });
            if (await _db.MasterListItems.AnyAsync(i => i.ListKey == key && i.Value == value))
                return Conflict(new { error = $"'{value}' is already in the list." });

            var max = await _db.MasterListItems.Where(i => i.ListKey == key)
                .Select(i => (int?)i.SortOrder).MaxAsync() ?? 0;
            if (dto.FrequencyYears is < 1 or > 20)
                return BadRequest(new { error = "Frequency must be between 1 and 20 years." });
            var item = new MasterListItem
            {
                ListKey = key, Value = value, IsActive = true, SortOrder = max + 1,
                FrequencyYears = key == MasterListKeys.SystemCategory ? dto.FrequencyYears : null
            };
            _db.MasterListItems.Add(item);
            await _db.SaveChangesAsync();
            return Ok(new { item.Id, item.Value, item.IsActive, item.FrequencyYears });
        }

        // PATCH /api/master/lists/items/9/frequency — set review frequency (years)
        [HttpPatch("lists/items/{id}/frequency")]
        [Authorize(Roles = "Corporate")]
        public async Task<IActionResult> SetFrequency(int id, [FromBody] FrequencyUpdateDto dto)
        {
            if (dto.FrequencyYears is < 1 or > 20)
                return BadRequest(new { error = "Frequency must be between 1 and 20 years." });
            var item = await _db.MasterListItems.FindAsync(id);
            if (item == null) return NotFound();
            item.FrequencyYears = dto.FrequencyYears;
            await _db.SaveChangesAsync();
            return Ok(new { item.Id, item.Value, item.IsActive, item.FrequencyYears });
        }

        // PATCH /api/master/lists/items/9/toggle-active
        [HttpPatch("lists/items/{id}/toggle-active")]
        [Authorize(Roles = "Corporate")]
        public async Task<IActionResult> ToggleListItem(int id)
        {
            var item = await _db.MasterListItems.FindAsync(id);
            if (item == null) return NotFound();
            item.IsActive = !item.IsActive;
            await _db.SaveChangesAsync();
            return Ok(new { item.Id, item.Value, item.IsActive });
        }
    }
}
