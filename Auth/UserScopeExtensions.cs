using System.Security.Claims;
using SiteReportApp.Models;
using SiteReportApp.Services;

namespace SiteReportApp.Auth
{
    // Small helpers so every controller can enforce site scoping in one line:
    //   if (!User.CanAccessSite(siteId)) return Forbid();
    public static class UserScopeExtensions
    {
        public static bool IsCorporate(this ClaimsPrincipal user) =>
            user.IsInRole(UserRole.Corporate.ToString());

        // The site a SiteUser is bound to (null for corporate users).
        public static int? GetSiteId(this ClaimsPrincipal user)
        {
            var raw = user.FindFirstValue(AuthService.SiteIdClaim);
            return int.TryParse(raw, out var id) ? id : null;
        }

        public static string GetDisplayName(this ClaimsPrincipal user) =>
            user.FindFirstValue("displayName") ?? user.Identity?.Name ?? "Unknown";

        // Corporate can touch any site; a site user only their own.
        public static bool CanAccessSite(this ClaimsPrincipal user, int siteId) =>
            user.IsCorporate() || user.GetSiteId() == siteId;
    }
}
