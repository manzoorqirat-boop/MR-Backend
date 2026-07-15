using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SiteReportApp.Auth;
using SiteReportApp.Data;
using SiteReportApp.Models;
using SiteReportApp.Services;

namespace SiteReportApp.Controllers
{
    public class LoginRequestDto
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public class UserCreateDto
    {
        public string Username { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string Password { get; set; } = string.Empty;
        public string Role { get; set; } = "SiteUser";   // "SiteUser" | "Corporate"
        public int? SiteId { get; set; }
    }

    public class PasswordResetDto
    {
        public string NewPassword { get; set; } = string.Empty;
    }

    public class EmailUpdateDto
    {
        public string? Email { get; set; }
    }

    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly AuthService _auth;
        private readonly AppDbContext _db;

        public AuthController(AuthService auth, AppDbContext db)
        {
            _auth = auth;
            _db = db;
        }

        private static object ToUserDto(User u) => new
        {
            id = u.Id,
            username = u.Username,
            displayName = u.DisplayName,
            email = u.Email,
            role = u.Role.ToString(),
            siteId = u.SiteId,
            siteName = u.Site?.Name,
            siteCode = u.Site?.Code,
            isActive = u.IsActive
        };

        // POST /api/auth/login  body: { username, password }
        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<IActionResult> Login([FromBody] LoginRequestDto request)
        {
            var result = await _auth.LoginAsync(request.Username, request.Password);
            if (result == null)
                return Unauthorized(new { error = "Invalid username or password." });

            var (user, token) = result.Value;
            return Ok(new { token, user = ToUserDto(user) });
        }

        // GET /api/auth/me — re-validate the stored token on app load
        [HttpGet("me")]
        [Authorize]
        public async Task<IActionResult> Me()
        {
            var idRaw = User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value
                        ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(idRaw, out var id)) return Unauthorized();

            var user = await _db.Users.Include(u => u.Site).FirstOrDefaultAsync(u => u.Id == id && u.IsActive);
            return user == null ? Unauthorized() : Ok(ToUserDto(user));
        }

        // ---- Corporate-only user management ----

        // GET /api/auth/users
        [HttpGet("users")]
        [Authorize(Roles = "Corporate")]
        public async Task<IActionResult> GetUsers()
        {
            var users = await _db.Users.Include(u => u.Site)
                .OrderBy(u => u.Role).ThenBy(u => u.Username)
                .ToListAsync();
            return Ok(users.Select(ToUserDto));
        }

        // POST /api/auth/users  body: { username, displayName, password, role, siteId }
        [HttpPost("users")]
        [Authorize(Roles = "Corporate")]
        public async Task<IActionResult> CreateUser([FromBody] UserCreateDto dto)
        {
            var username = dto.Username.Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(dto.Password))
                return BadRequest(new { error = "Username and password are required." });
            if (dto.Password.Length < 8)
                return BadRequest(new { error = "Password must be at least 8 characters." });
            if (!Enum.TryParse<UserRole>(dto.Role, ignoreCase: true, out var role))
                return BadRequest(new { error = $"Invalid role '{dto.Role}'." });
            if (role == UserRole.SiteUser && dto.SiteId == null)
                return BadRequest(new { error = "Site users must be assigned to a site." });
            if (await _db.Users.AnyAsync(u => u.Username == username))
                return Conflict(new { error = $"Username '{username}' is already taken." });
            if (dto.SiteId.HasValue && !await _db.Sites.AnyAsync(s => s.Id == dto.SiteId.Value))
                return BadRequest(new { error = "Selected site does not exist." });

            var user = new User
            {
                Username = username,
                DisplayName = string.IsNullOrWhiteSpace(dto.DisplayName) ? username : dto.DisplayName.Trim(),
                Email = string.IsNullOrWhiteSpace(dto.Email) ? null : dto.Email.Trim(),
                PasswordHash = AuthService.HashPassword(dto.Password),
                Role = role,
                SiteId = role == UserRole.Corporate ? null : dto.SiteId,
                IsActive = true
            };
            _db.Users.Add(user);
            await _db.SaveChangesAsync();
            await _db.Entry(user).Reference(u => u.Site).LoadAsync();
            return Ok(ToUserDto(user));
        }

        // PATCH /api/auth/users/5/toggle-active — deactivate/reactivate a login
        [HttpPatch("users/{id}/toggle-active")]
        [Authorize(Roles = "Corporate")]
        public async Task<IActionResult> ToggleActive(int id)
        {
            var user = await _db.Users.Include(u => u.Site).FirstOrDefaultAsync(u => u.Id == id);
            if (user == null) return NotFound();
            user.IsActive = !user.IsActive;
            await _db.SaveChangesAsync();
            return Ok(ToUserDto(user));
        }

        // PATCH /api/auth/users/5/password  body: { newPassword }
        [HttpPatch("users/{id}/password")]
        [Authorize(Roles = "Corporate")]
        public async Task<IActionResult> ResetPassword(int id, [FromBody] PasswordResetDto dto)
        {
            if (dto.NewPassword.Length < 8)
                return BadRequest(new { error = "Password must be at least 8 characters." });
            var user = await _db.Users.FindAsync(id);
            if (user == null) return NotFound();
            user.PasswordHash = AuthService.HashPassword(dto.NewPassword);
            await _db.SaveChangesAsync();
            return Ok(new { ok = true });
        }
    }
}
