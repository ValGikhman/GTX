using GTX.Models;
using Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace GTX.Controllers {

    public class MajordomeController : BaseController {

        public MajordomeController(ISessionData sessionData, ILogService logService)
            : base(sessionData, logService) {
            if (Model == null) {
                Model = new BaseModel();
                Model.Inventory = new Inventory();
            }
        }

        public ActionResult Index(BaseModel model) {
            Model.Inventory.Vehicles = Model.Inventory.All;
            return View(Model);
        }

        public ActionResult Inventory(BaseModel model) {
            if (Model.Inventory.Vehicles == null) {
                Model.Inventory.Vehicles = Model.Inventory.All;
            }

            return View(Model);
        }

        public ActionResult Logs() {
            return View(LogService.GetLogs());
        }

        [HttpGet]
        public JsonResult GetUpdatedItems() {
            var items = Model.Inventory.All; // updated list
            return Json(items, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        public ActionResult Upload(List<HttpPostedFileBase> files, string stock) {
            if (files == null || files.Count == 0) {
                return new HttpStatusCodeResult(400, "No files uploaded.");
            }

            var uploadPath = Server.MapPath("~/GTXImages/Inventory/" + stock);
            if (!Directory.Exists(uploadPath)) {
                Directory.CreateDirectory(uploadPath);
            }

            foreach (var file in files) {
                if (file != null && file.ContentLength > 0) {
                    var filePath = Path.Combine(uploadPath, Path.GetFileName(file.FileName));
                    file.SaveAs(filePath);
                }
            }

            Model.Inventory.Vehicles = ApplyImages(Model.Inventory.Vehicles);
            return Json(new { Message = "Upload successful", FileCount = files.Count });
        }

        [HttpPost]
        public JsonResult ApplyTerm(string term) {
            term = term.Trim().ToUpper();
            Log($"Applying term: {term}");
            Model.CurrentFilter = null;
            Model.Inventory.Vehicles = ApplyTerms(term);
            Model.Inventory.Title = "Search";
            return Json(new { redirectUrl = Url.Action("Inventory") });
        }

        [HttpPost]
        public ActionResult Reset() {
            Model.Inventory.Vehicles = Model.Inventory.All;
            return Json(new { redirectUrl = Url.Action("Inventory") });
        }

        [HttpPost]
        public ActionResult DeleteImages(string stock) {
            string path = $"~/GTXImages/Inventory/{stock}";
            path = Server.MapPath(path);

            string[] extensions = new[] { ".jpg", ".jpeg", ".png", ".gif" };

            if (!Directory.Exists(path)) {
                return Json(new { success = false, message = "Directory not found." });
            }

            try {
                string[] imageFiles = Directory.GetFiles(path).Where(file => extensions.Contains(Path.GetExtension(file).ToLower())).ToArray();

                foreach (string file in imageFiles) {
                    System.IO.File.Delete(file);
                }


                Model.Inventory.Vehicles = ApplyImages(Model.Inventory.Vehicles);
                return Json(new { success = true, message = "All files deleted successfully." });
            }

            catch (Exception ex) {
                return Json(new { success = false, message = "Error: " + ex.Message });
            }

        }

        private Models.GTX[] ApplyTerms(string term) {
            Models.GTX[] query = Model.Inventory.All;

            if (query.Any() && term != null) {
                query = query.Where(m => m.Stock.ToUpper().Contains(term)
                    || (m.Year.ToString() == term)
                    || m.Make.ToUpper().Contains(term)
                    || m.Model.ToUpper().Contains(term)
                    || m.VehicleStyle.ToUpper().Contains(term))
                .Distinct().ToArray();
            }

            return query.OrderBy(m => m.Make).ToArray();
        }

    }
}