using System.Web.Mvc;

namespace GTX.Controllers {

    public class HomeController : Controller {

        private readonly IUserService _userService;

        public HomeController(IUserService userService) {
            _userService = userService;
        }

        public ActionResult Index() {
            ViewBag.Message = "Home";
            ViewBag.Title = "Home";

            return View();
        }

        public ActionResult About() {
            ViewBag.Message = "About";
            ViewBag.Title = "About us";

            return View();
        }

        public ActionResult Staff() {
            ViewBag.Message = "Staff";
            ViewBag.Title = "Our staff";

            return View();
        }

        public ActionResult Contact() {
            ViewBag.Message = "Contact";
            ViewBag.Title = "Contact us";

            return View();
        }
        public ActionResult Application() {
            ViewBag.Message = "Application";

            return View();
        }

        public ActionResult Blog() {
            ViewBag.Message = "Blog";
            ViewBag.Title = "Latest news";

            return View();
        }
    }
}