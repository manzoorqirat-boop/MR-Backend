namespace SiteReportApp.Models
{
    // Evidence file attached to an initiative (before/after photos, SOPs,
    // approval mails, etc.). Bytes live in Postgres because the app container's
    // filesystem is ephemeral on Railway.
    public class InitiativeAttachment
    {
        public int Id { get; set; }
        public int InitiativeId { get; set; }
        public Initiative Initiative { get; set; } = null!;
        public string FileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = "application/octet-stream";
        public long SizeBytes { get; set; }
        public byte[] Data { get; set; } = Array.Empty<byte>();
        public string UploadedBy { get; set; } = string.Empty;
        public DateTime UploadedAtUtc { get; set; } = DateTime.UtcNow;
    }
}
