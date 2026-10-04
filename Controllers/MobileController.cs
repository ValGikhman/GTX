using System.IO;
using System.Web.Mvc;

namespace GTX.Controllers {
    [AllowAnonymous]
    public sealed class MobileController : Controller {
        private const string ApkName = "GTX-Majordome-1.0.0.apk";
        private string ApkPath => Server.MapPath("~/App_Data/Mobile/" + ApkName);

        [HttpGet]
        public ActionResult Index() {
            ViewBag.ApkAvailable = System.IO.File.Exists(ApkPath);
            ViewBag.ApkSize = ViewBag.ApkAvailable
                ? (new FileInfo(ApkPath).Length / 1048576d).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)
                : null;
            return View();
        }

        [HttpGet]
        public ActionResult Download() {
            if (!System.IO.File.Exists(ApkPath)) return HttpNotFound("The Android download is not available yet.");
            Response.Cache.SetCacheability(System.Web.HttpCacheability.NoCache);
            Response.AddHeader("Content-Length", new FileInfo(ApkPath).Length.ToString(System.Globalization.CultureInfo.InvariantCulture));
            return File(ApkPath, "application/vnd.android.package-archive", ApkName);
        }
    }
}
