using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SiteReportApp.Auth;
using Microsoft.EntityFrameworkCore;
using SiteReportApp.Data;
using SiteReportApp.Dtos;
using SiteReportApp.Models;
using SiteReportApp.Services;

namespace SiteReportApp.Controllers
{
    [ApiController]
    [Route("api/initiatives")]
    [Authorize]
    public class InitiativesController : ControllerBase
    {
        private readonly DataEntryService _entry;
        private readonly AppDbContext _db;

        public InitiativesController(DataEntryService entry, AppDbContext db)
        {
            _entry = entry;
            _db = db;
        }

        // GET /api/initiatives?siteId=1&reportPeriodId=5&type=LeanLaboratory
        // Added so the frontend can show previously saved rows (and their ids, for delete).
        // Each row carries its attachment count so the list can show 📎 badges.
        [HttpGet]
        public async Task<IActionResult> GetForSiteAndPeriod(
            [FromQuery] int siteId, [FromQuery] int reportPeriodId, [FromQuery] string type)
        {
            if (!User.CanAccessSite(siteId)) return Forbid();
            if (!Enum.TryParse<InitiativeType>(type, ignoreCase: true, out var parsedType))
                return BadRequest(new { error = $"Invalid initiative type: '{type}'" });

            var data = await _db.Initiatives
                .Where(i => i.SiteId == siteId && i.ReportPeriodId == reportPeriodId && i.Type == parsedType)
                .OrderBy(i => i.SerialNo)
                .Select(i => new
                {
                    i.Id, i.SiteId, i.ReportPeriodId, i.Type, i.SerialNo, i.Name, i.Department,
                    i.Category, i.FacilitatorName, i.DepartmentHead, i.Status, i.Remarks,
                    AttachmentCount = _db.InitiativeAttachments.Count(a => a.InitiativeId == i.Id)
                })
                .ToListAsync();
            return Ok(data);
        }

        // PUT /api/initiatives/5 — workflow: progress status / edit details in place
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] InitiativeUpdateDto dto)
        {
            var entity = await _db.Initiatives.AsNoTracking().FirstOrDefaultAsync(i => i.Id == id);
            if (entity == null) return NotFound();
            if (!User.CanAccessSite(entity.SiteId)) return Forbid();
            try
            {
                var updated = await _entry.UpdateInitiativeAsync(id, dto);
                return Ok(updated);
            }
            catch (KeyNotFoundException) { return NotFound(); }
            catch (InvalidOperationException ex) { return Conflict(new { error = ex.Message }); }
        }

        // ==================== Attachments (evidence files) ====================
        private const long MaxAttachmentBytes = 10 * 1024 * 1024; // 10 MB
        private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".png", ".jpg", ".jpeg", ".csv", ".txt", ".msg", ".eml" };

        // GET /api/initiatives/5/attachments — metadata only (no bytes)
        [HttpGet("{id}/attachments")]
        public async Task<IActionResult> GetAttachments(int id)
        {
            var initiative = await _db.Initiatives.AsNoTracking().FirstOrDefaultAsync(i => i.Id == id);
            if (initiative == null) return NotFound();
            if (!User.CanAccessSite(initiative.SiteId)) return Forbid();

            var files = await _db.InitiativeAttachments
                .Where(a => a.InitiativeId == id)
                .OrderByDescending(a => a.UploadedAtUtc)
                .Select(a => new { a.Id, a.FileName, a.ContentType, a.SizeBytes, a.UploadedBy, a.UploadedAtUtc })
                .ToListAsync();
            return Ok(files);
        }

        // POST /api/initiatives/5/attachments — multipart form, field name "file"
        [HttpPost("{id}/attachments")]
        [RequestSizeLimit(MaxAttachmentBytes + 1024 * 1024)]
        public async Task<IActionResult> UploadAttachment(int id, IFormFile file)
        {
            var initiative = await _db.Initiatives.AsNoTracking().FirstOrDefaultAsync(i => i.Id == id);
            if (initiative == null) return NotFound();
            if (!User.CanAccessSite(initiative.SiteId)) return Forbid();

            if (file == null || file.Length == 0)
                return BadRequest(new { error = "No file uploaded." });
            if (file.Length > MaxAttachmentBytes)
                return BadRequest(new { error = "File is larger than the 10 MB limit." });
            var ext = Path.GetExtension(file.FileName);
            if (!AllowedExtensions.Contains(ext))
                return BadRequest(new { error = $"File type '{ext}' is not allowed. Allowed: {string.Join(", ", AllowedExtensions)}" });

            try
            {
                // Same freeze rules as data entry: no uploads once the month is
                // submitted/approved or the period locked.
                await _entry.EnsureSiteEditableAsync(initiative.SiteId, initiative.ReportPeriodId);
            }
            catch (InvalidOperationException ex) { return Conflict(new { error = ex.Message }); }

            using var ms = new MemoryStream();
            await file.CopyToAsync(ms);
            var attachment = new InitiativeAttachment
            {
                InitiativeId = id,
                FileName = Path.GetFileName(file.FileName),
                ContentType = string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType,
                SizeBytes = file.Length,
                Data = ms.ToArray(),
                UploadedBy = User.GetDisplayName(),
                UploadedAtUtc = DateTime.UtcNow
            };
            _db.InitiativeAttachments.Add(attachment);
            await _db.SaveChangesAsync();

            return Ok(new { attachment.Id, attachment.FileName, attachment.ContentType, attachment.SizeBytes, attachment.UploadedBy, attachment.UploadedAtUtc });
        }

        // GET /api/initiatives/attachments/42/download — the file itself.
        // Downloads stay available after submission/approval (corporate reviews them).
        [HttpGet("attachments/{attachmentId}/download")]
        public async Task<IActionResult> DownloadAttachment(int attachmentId)
        {
            var attachment = await _db.InitiativeAttachments
                .Include(a => a.Initiative)
                .FirstOrDefaultAsync(a => a.Id == attachmentId);
            if (attachment == null) return NotFound();
            if (!User.CanAccessSite(attachment.Initiative.SiteId)) return Forbid();

            return File(attachment.Data, attachment.ContentType, attachment.FileName);
        }

        // DELETE /api/initiatives/attachments/42 — blocked once the month is frozen
        [HttpDelete("attachments/{attachmentId}")]
        public async Task<IActionResult> DeleteAttachment(int attachmentId)
        {
            var attachment = await _db.InitiativeAttachments
                .Include(a => a.Initiative)
                .FirstOrDefaultAsync(a => a.Id == attachmentId);
            if (attachment == null) return NotFound();
            if (!User.CanAccessSite(attachment.Initiative.SiteId)) return Forbid();

            try
            {
                await _entry.EnsureSiteEditableAsync(attachment.Initiative.SiteId, attachment.Initiative.ReportPeriodId);
            }
            catch (InvalidOperationException ex) { return Conflict(new { error = ex.Message }); }

            _db.InitiativeAttachments.Remove(attachment);
            await _db.SaveChangesAsync();
            return NoContent();
        }

        // POST /api/initiatives/bulk
        // body: { siteId, reportPeriodId, type, rows: [...] }
        [HttpPost("bulk")]
        public async Task<IActionResult> SaveBulk([FromBody] InitiativeBulkCreateDto request)
        {
            if (!User.CanAccessSite(request.SiteId)) return Forbid();
            try
            {
                var result = await _entry.SaveInitiativesAsync(request);
                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { error = ex.Message });
            }
        }

        // DELETE /api/initiatives/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var entity = await _db.Initiatives.FindAsync(id);
            if (entity != null && !User.CanAccessSite(entity.SiteId)) return Forbid();
            try
            {
                await _entry.DeleteInitiativeAsync(id);
                return NoContent();
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { error = ex.Message });
            }
        }
    }
}
