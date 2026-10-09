using System.Web.Mvc;
using System.Web.Routing;

namespace GTX.CarCare
{
    public static class RouteConfig
    {
        public static void RegisterRoutes(RouteCollection routes)
        {
            routes.IgnoreRoute("{resource}.axd/{*pathInfo}");
            routes.MapRoute("Services", "services", new { controller = "Home", action = "Services" });
            routes.MapRoute("Home", "", new { controller = "Home", action = "Index" });
        }
    }
}
