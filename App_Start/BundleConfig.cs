using System.Web;
using System.Web.Optimization;

namespace GTX {
    public class BundleConfig {
        // For more information on bundling, visit https://go.microsoft.com/fwlink/?LinkId=301862
        public static void RegisterBundles(BundleCollection bundles) {
            bundles.Add(new ScriptBundle("~/Scripts/jquery").Include(
                        "~/Scripts/jquery/jquery.magnific-popup.min.js",
                        "~/Scripts/jquery/jquery.slicknav.js",
                        "~/Scripts/jquery/jquery-{version}.js",
                        "~/Scripts/jquery/jquery-ui.min.js",
                        "~/Scripts/bootstrap/bootstrap-datepicker.min.js",
                        "~/Scripts/main.js",
                        "~/Scripts/chosen/chosen.jquery.js"
                )
            );

            // Use the development version of Modernizr to develop with and learn from. Then, when you're
            // ready for production, use the build tool at https://modernizr.com to pick only the tests you need.
            bundles.Add(new ScriptBundle("~/Scripts/modernizr").Include(
                        "~/Scripts/modernizr-*"));

            bundles.Add(new Bundle("~/Scripts/bootstrap").Include(
                      "~/Scripts/bootstrap/bootstrap.bundle.min.js"));

            bundles.Add(new StyleBundle("~/Content/css").Include(
                      "~/Content/jquery/jquery-ui.min.css",
                      "~/Content/bootstrap/bootstrap.css",
                      "~/Content/bootstrap/bootstrap-datepicker.css",
                      "~/Content/elegant-icons.css",
                      "~/Content/font-awesome.min.css",
                      "~/Content/magnific-popup.css",
                      "~/Content/owl.carousel.min.css",
                      "~/Content/slicknav.min.css",
                      "~/Content/Site.css",
                      "~/Scripts/chosen/chosen.min.css"
                ));
        }
    }
}
