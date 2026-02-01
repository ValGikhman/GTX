using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Web;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;

namespace GTX
{
    public class MvcApplication : System.Web.HttpApplication
    {
        protected void Application_Start()
        {
            AreaRegistration.RegisterAllAreas();
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            BundleConfig.RegisterBundles(BundleTable.Bundles);
            UnityConfig.RegisterComponents();

            Application["TotalSessions"] = 0;
        }

        protected void Application_BeginRequest(object sender, EventArgs e)
        {
            var path = (Request.Path ?? "").ToLowerInvariant();
            if (path == "/out-of-service.html") return; // prevent loop
            if (path.StartsWith("/health")) return;
            if (path.StartsWith("/content") || path.StartsWith("/scripts") || path.StartsWith("/bundles")) return;

            if (MaintenanceFlag.IsOffline())
            {
                Response.Redirect("/out-of-service.html", true);
            }

            var host = HttpContext.Current.Request.Url.Host;
            if (host.Equals("admin.usedcarscincinnati.com", StringComparison.OrdinalIgnoreCase)) HttpContext.Current.Response.Redirect("https://usedcarscincinnati.com/Majordome/Inventory", true);

            var culture = CultureHelper.GetCultureFromRequest(Request);
            CultureHelper.ApplyCulture(culture);
        }

        protected void Session_Start()
        {
            Application.Lock();
            Application["TotalSessions"] = ((int)Application["TotalSessions"]) + 1;
            Application.UnLock();
        }

        protected void Session_End()
        {
            Application.Lock();
            Application["TotalSessions"] = Math.Max(0, ((int)Application["TotalSessions"]) - 1);
            Application.UnLock();
        }
    }

    public static class MaintenanceFlag
    {
        // Put the flag in App_Data (not publicly served)
        private const string FlagRelativePath = "~/App_Data/maintenance.flag";

        public static bool IsOffline()
        {
            var fullPath = HttpContext.Current.Server.MapPath(FlagRelativePath);
            return File.Exists(fullPath);
        }

        public static void SetOffline(bool offline)
        {
            var fullPath = HttpContext.Current.Server.MapPath(FlagRelativePath);

            if (offline)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
                if (!File.Exists(fullPath)) File.WriteAllText(fullPath, "offline");
            }
            else
            {
                if (File.Exists(fullPath)) File.Delete(fullPath);
            }
        }
    }

    public static class CultureHelper
    {
        private static readonly string[] Supported = { "en", "es" }; // add more later
        private const string DefaultCulture = "en";

        public static string GetCultureFromRequest(HttpRequest request)
        {
            // 1) querystring (optional): ?lang=es
            var qs = request.QueryString["lang"];
            if (!string.IsNullOrWhiteSpace(qs))
                return Normalize(qs);

            // 2) cookie (preferred)
            var cookie = request.Cookies["lang"]?.Value;
            if (!string.IsNullOrWhiteSpace(cookie))
                return Normalize(cookie);

            // 3) browser fallback (optional)
            var al = request.UserLanguages?.FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(al))
                return Normalize(al);

            return DefaultCulture;
        }

        public static void ApplyCulture(string culture)
        {
            var ci = new CultureInfo(culture);
            Thread.CurrentThread.CurrentCulture = ci;
            Thread.CurrentThread.CurrentUICulture = ci;
        }

        private static string Normalize(string raw)
        {
            var lang = (raw ?? "").Trim().ToLowerInvariant();

            if (lang.Contains(",")) lang = lang.Split(',')[0];
            if (lang.Contains(";")) lang = lang.Split(';')[0];
            if (lang.Contains("-")) lang = lang.Split('-')[0]; // es-ES -> es

            if (!Supported.Contains(lang)) lang = DefaultCulture;
            return lang;
        }
    }
}
