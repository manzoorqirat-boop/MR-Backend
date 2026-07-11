using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SiteReportApp.Data;
using SiteReportApp.Models;

namespace SiteReportApp.Services
{
    public class AuthService
    {
        private readonly AppDbContext _db;
        private readonly IConfiguration _config;

        public const string SiteIdClaim = "siteId";

        public AuthService(AppDbContext db, IConfiguration config)
        {
            _db = db;
            _config = config;
        }

        // ---- Signing key ----
        // JWT_SECRET should be set in production (Railway env var). The fallback keeps
        // local dev working, but tokens minted with it are worthless elsewhere.
        public static string GetSecret() =>
            Environment.GetEnvironmentVariable("JWT_SECRET")
            ?? "dev-only-secret-change-me-in-production-0123456789";

        public static SymmetricSecurityKey GetSigningKey() =>
            new(SHA256.HashData(Encoding.UTF8.GetBytes(GetSecret())));

        // ---- Password hashing: PBKDF2 (no extra packages needed) ----
        public static string HashPassword(string password)
        {
            const int iterations = 100_000;
            var salt = RandomNumberGenerator.GetBytes(16);
            var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, 32);
            return $"{iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
        }

        public static bool VerifyPassword(string password, string stored)
        {
            var parts = stored.Split('.');
            if (parts.Length != 3) return false;
            if (!int.TryParse(parts[0], out var iterations)) return false;
            var salt = Convert.FromBase64String(parts[1]);
            var expected = Convert.FromBase64String(parts[2]);
            var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }

        // ---- Login ----
        public async Task<(User user, string token)?> LoginAsync(string username, string password)
        {
            var normalized = username.Trim().ToLowerInvariant();
            var user = await _db.Users
                .Include(u => u.Site)
                .FirstOrDefaultAsync(u => u.Username == normalized && u.IsActive);

            if (user == null || !VerifyPassword(password, user.PasswordHash)) return null;

            var claims = new List<Claim>
            {
                new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new(ClaimTypes.Name, user.Username),
                new("displayName", user.DisplayName),
                new(ClaimTypes.Role, user.Role.ToString())
            };
            if (user.SiteId.HasValue)
                claims.Add(new Claim(SiteIdClaim, user.SiteId.Value.ToString()));

            var creds = new SigningCredentials(GetSigningKey(), SecurityAlgorithms.HmacSha256);
            var token = new JwtSecurityToken(
                issuer: "SiteReportApp",
                audience: "SiteReportApp",
                claims: claims,
                expires: DateTime.UtcNow.AddHours(12),
                signingCredentials: creds);

            return (user, new JwtSecurityTokenHandler().WriteToken(token));
        }

        // ---- First-boot seeding ----
        // Creates a corporate admin if no users exist so the app is never locked out.
        // Override the default password with the ADMIN_PASSWORD env var.
        public async Task SeedAdminAsync()
        {
            if (await _db.Users.AnyAsync()) return;
            var password = Environment.GetEnvironmentVariable("ADMIN_PASSWORD") ?? "Admin@123";
            _db.Users.Add(new User
            {
                Username = "admin",
                DisplayName = "Corporate Admin",
                PasswordHash = HashPassword(password),
                Role = UserRole.Corporate,
                SiteId = null,
                IsActive = true
            });
            await _db.SaveChangesAsync();
        }
    }
}
