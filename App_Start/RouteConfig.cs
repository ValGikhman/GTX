using System.Web.Mvc;
using System.Web.Routing;

namespace GTX {
    public class RouteConfig {
        public static void RegisterRoutes(RouteCollection routes) {
            routes.IgnoreRoute("{resource}.axd/{*pathInfo}");
            routes.MapRoute(
                name: "TestDriveContact",
                url: "Test-Drive",
                defaults: new { controller = "Home", action = "Contact", testDrive = true }
            );

            routes.MapRoute(
                name: "Inventory",
                url: "Majordome/Inventory/{stock}",
                defaults: new { controller = "Majordome", action = "Inventory", stock = UrlParameter.Optional }
            );

            routes.MapRoute(
                name: "VehicleSeo",
                url: "inventory/{year}-{name}-{stock}",
                defaults: new { controller = "Inventory", action = "Details" }
            );

            routes.MapRoute(
                name: "Default",
                url: "{controller}/{action}/{id}",
                defaults: new { controller = "Home", action = "Index", id = UrlParameter.Optional }
            );

        }
    }
}
