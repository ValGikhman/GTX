using GTX.Models;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Mvc;

namespace GTX.Controllers {

    public class InventoryController : Controller {

        public ActionResult Index() {
            ViewBag.Message = "Inventory page.";

            return View();
        }
    }
}