using System;
using System.IO;
using System.Web;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;

namespace GTX {
    public class MvcApplication : System.Web.HttpApplication {
        protected void Application_Start() {
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
                if (!File.Exists(fullPath))         File.WriteAllText(fullPath, "offline");
            }
            else
            {
                if (File.Exists(fullPath))  File.Delete(fullPath);
            }
        }
    }

}
