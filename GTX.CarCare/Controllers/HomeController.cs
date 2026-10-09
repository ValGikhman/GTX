using System.Web.Mvc;
using GTX.CarCare.Models;

namespace GTX.CarCare.Controllers
{
    public sealed class HomeController : Controller
    {
        public ActionResult Index()
        {
            return View(CarCareContent.Services);
        }

        public ActionResult Services()
        {
            return View(CarCareContent.Services);
        }
    }
}
