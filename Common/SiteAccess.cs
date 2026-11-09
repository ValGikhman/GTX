using GTX.Models;
using System;
using System.Linq;
using System.Text;
using System.Web;
using System.Web.Security;

namespace GTX.Common {
    public static class SiteAccess {
        private const string CookieName = "gtx_access";
        public static SiteRoleModel Current(HttpRequestBase request) {
            var value = request.Cookies[CookieName]?.Value;
            if (!string.IsNullOrEmpty(value)) {
                string ticket;
                try { ticket = Encoding.UTF8.GetString(MachineKey.Unprotect(Convert.FromBase64String(value), "GTX.SiteRole")); }
                catch { return null; }
                var parts = ticket.Split(':');
                int id;
                if (parts.Length != 2 || !int.TryParse(parts[0], out id)) return null;
                return new SiteRoleStore().All().FirstOrDefault(r => r.Id == id && r.Revision == parts[1]);
            }
            return null;
        }
        public static bool Can(SiteRoleModel role, MajordomeItem item) { return role != null && (role.Name == "Owner" || (item != MajordomeItem.Roles && role.Permissions.Contains(item.ToString()))); }
        public static void SignIn(SiteRoleModel role, HttpRequestBase request, HttpResponseBase response, HttpSessionStateBase session, bool persist) {
            var ticket = Convert.ToBase64String(MachineKey.Protect(Encoding.UTF8.GetBytes(role.Id + ":" + role.Revision), "GTX.SiteRole"));
            var cookie = new HttpCookie(CookieName, ticket) { HttpOnly=true, Secure=request.IsSecureConnection, Path="/", SameSite=SameSiteMode.Lax };
            if (persist) cookie.Expires = DateTime.UtcNow.AddDays(30);
            response.Cookies.Add(cookie);
            CommonUnit.Roles legacy;
            if (!Enum.TryParse(role.Name, true, out legacy)) legacy = (CommonUnit.Roles)role.Id;
            session["CurrentRole"] = legacy;
            session["SiteRoleName"] = role.Name;
            session["SiteRolePermissions"] = role.Permissions;
            RoleCookie.Set(response, legacy.ToString(), persist);
        }
        public static void Clear(HttpResponseBase response) { response.Cookies.Add(new HttpCookie(CookieName, "") { Expires=DateTime.UtcNow.AddDays(-1), Path="/" }); }
    }
}
