using GTX.Models;
using System;
using System.Linq;
using System.Text;
using System.Web;
using System.Web.Security;

namespace GTX.Common {
    public static class SiteAccess {
        private const string CookieName = "gtx_access";
        private const string RequestCacheKey = "GTX.SiteAccess";
        private sealed class RequestRole {
            public string CookieValue { get; set; }
            public SiteRoleModel Role { get; set; }
        }
        public static SiteRoleModel Current(HttpRequestBase request) {
            var value = request.Cookies[CookieName]?.Value;
            var context = HttpContext.Current;
            var cached = context?.Items[RequestCacheKey] as RequestRole;
            if (cached != null && string.Equals(cached.CookieValue, value, StringComparison.Ordinal))
                return cached.Role;

            var role = Resolve(value);
            if (context != null) context.Items[RequestCacheKey] = new RequestRole { CookieValue = value, Role = role };
            return role;
        }
        private static SiteRoleModel Resolve(string value) {
            if (!string.IsNullOrEmpty(value)) {
                string ticket;
                try { ticket = Encoding.UTF8.GetString(MachineKey.Unprotect(Convert.FromBase64String(value), "GTX.SiteRole")); }
                catch { return null; }
                var parts = ticket.Split(':');
                int id;
                if (parts.Length != 2 || !int.TryParse(parts[0], out id)) return null;
                var role = id == MajordomeAccount.Id ? MajordomeAccount.Current()
                    : id > 0 ? new SiteRoleStore().All().FirstOrDefault(r => r.Id == id) : null;
                return role != null && role.Revision == parts[1] ? role : null;
            }
            return null;
        }
        public static bool IsMajordome(SiteRoleModel role) { return MajordomeAccount.IsMajordome(role); }
        public static bool Can(SiteRoleModel role, MajordomeItem item) {
            if (role == null || !MajordomeMenu.Items.Any(i => i.Item == item)) return false;
            if (IsMajordome(role)) return true;
            if (role.Id < 0 || MajordomeAccount.IsReservedName(role.Name)) return false;
            return item != MajordomeItem.Roles && item != MajordomeItem.Profile && item != MajordomeItem.Services
                && (role.Permissions ?? new string[0]).Contains(item.ToString());
        }
        public static void SignIn(SiteRoleModel role, HttpRequestBase request, HttpResponseBase response, HttpSessionStateBase session, bool persist) {
            HttpContext.Current?.Items.Remove(RequestCacheKey);
            var ticket = Convert.ToBase64String(MachineKey.Protect(Encoding.UTF8.GetBytes(role.Id + ":" + role.Revision), "GTX.SiteRole"));
            var cookie = new HttpCookie(CookieName, ticket) { HttpOnly=true, Secure=request.IsSecureConnection, Path="/", SameSite=SameSiteMode.Lax };
            if (persist) cookie.Expires = DateTime.UtcNow.AddDays(30);
            response.Cookies.Add(cookie);
            CommonUnit.Roles legacy;
            if (!Enum.TryParse(role.Name, true, out legacy)) legacy = (CommonUnit.Roles)role.Id;
            session["CurrentRole"] = legacy;
            session["SiteRoleName"] = role.Name;
            session["SiteRolePermissions"] = MajordomeMenu.Items.Where(i => Can(role, i.Item)).Select(i => i.Item.ToString()).ToArray();
            RoleCookie.Set(response, legacy.ToString(), persist);
        }
        public static void Clear(HttpResponseBase response) {
            HttpContext.Current?.Items.Remove(RequestCacheKey);
            response.Cookies.Add(new HttpCookie(CookieName, "") { Expires=DateTime.UtcNow.AddDays(-1), Path="/" });
        }
    }
}
