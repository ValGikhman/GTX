using System;
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
}
