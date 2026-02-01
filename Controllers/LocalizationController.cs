using System;
using System.Web;
using System.Web.Mvc;

namespace GTX.Controllers
{
    public class LocalizationController : Controller
    {
        [HttpPost]
        public ActionResult SetLanguage(string lang)
        {
            var cookie = new HttpCookie("lang", lang)
            {
                Expires = DateTime.UtcNow.AddYears(1),
                HttpOnly = false,
                Path = "/", // site-wide
                SameSite = SameSiteMode.Lax
            };

            // ✅ THIS is what you missed:
            Response.Cookies.Set(cookie);

            // optional: if you want the rest of *this same request* to see it
            // (not needed for redirect, but harmless)
            Request.Cookies.Set(cookie);

            return RedirectToAction("Index", "Home");
        }
    }
}