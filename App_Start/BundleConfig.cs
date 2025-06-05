using System.Web;
using System.Web.Optimization;

namespace GTX {
    public class BundleConfig {
        // For more information on bundling, visit https://go.microsoft.com/fwlink/?LinkId=301862
        public static void RegisterBundles(BundleCollection bundles) {
            bundles.Add(new ScriptBundle("~/bundles/jquery").Include(
                        "~/Scripts/jquery/jquery-ui.min.js",
                        "~/Scripts/mixitup.min.js",
                        "~/Scripts/owl.carousel.min.js",
                        "~/Scripts/main.js",
                        "~/Scripts/chosen/chosen.jquery.js",
                        "~/Scripts/bootstrap/bootstrap-datepicker.min.js",
                        "~/Scripts/jquery/jquery.validate.min.js"
                )
            );

            // Use the development version of Modernizr to develop with and learn from. Then, when you're
            // ready for production, use the build tool at https://modernizr.com to pick only the tests you need.
            bundles.Add(new ScriptBundle("~/bundles/modernizr").Include(
                        "~/Scripts/modernizr-*"));

            bundles.Add(new Bundle("~/bundles/bootstrap").Include(
                      "~/Scripts/bootstrap/bootstrap.min.js"));

            bundles.Add(new StyleBundle("~/Content/css").Include(
                      "~/Content/bootstrap/bootstrap.css",
                      "~/Content/bootstrap/bootstrap-datepicker.css",
                      "~/Content/elegant-icons.css",
                      "~/Content/font-awesome.min.css",
                      "~/Content/jquery/jquery-ui.min.css",
                      "~/Content/magnific-popup.css",
                      "~/Content/owl.carousel.min.css",
                      "~/Content/slicknav.min.css",
                      "~/Content/Site.css"
                ));
        }
    }
}
