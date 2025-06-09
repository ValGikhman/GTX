using GTX.Models;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Mvc;

namespace GTX.Controllers {

    public class InventoryController : BaseController {

        public ActionResult All() {
            ViewBag.Message = "Inventory.";
            ViewBag.Title = "Inventory";

            return View();
        }
    }
}