namespace SiteReportApp.Models
{
    // Application login. Site users are bound to exactly one Site (SiteId != null)
    // and can only enter/see data for that site. Corporate users (SiteId == null)
    // review submissions from every site and manage sites/periods/users.
    public class User
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;      // unique, case-insensitive
        public string DisplayName { get; set; } = string.Empty;
        public string? Email { get; set; }                        // reminder notifications go here
        public string PasswordHash { get; set; } = string.Empty;  // PBKDF2, format: iterations.saltB64.hashB64
        public UserRole Role { get; set; } = UserRole.SiteUser;
        public int? SiteId { get; set; }                          // required when Role == SiteUser
        public Site? Site { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    }
}
