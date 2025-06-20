using GTX.Models;
using Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;

namespace GTX.Controllers {

    public class MajordomeController : BaseController {

        public MajordomeController(ISessionData sessionData, ILogService LogService)
            : base(sessionData, LogService) {
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
            Log($"Applying term: {term.Trim().ToUpper()}");
            Model.Inventory.Vehicles = ApplyTerms(term.Trim().ToUpper());

            return Json(new { redirectUrl = Url.Action("Inventory") });
        }

        [HttpPost]
        public ActionResult Reset(BaseModel model) {
            Model.Inventory.Vehicles = Model.Inventory.All;
            return Json(new { redirectUrl = Url.Action("Inventory") });
        }

        private Models.GTX[] ApplyTerms(string term) {
            Models.GTX[] query = Model.Inventory.All;

            if (query.Any() && term != null) {
                query = query.Where(m => m.Stock.ToUpper().Contains(term) 
                    || m.Make.ToUpper().Contains(term) 
                    || m.Model.ToUpper().Contains(term) 
                    || m.VehicleStyle.ToUpper().Contains(term))
                .Distinct().ToArray();
            }

            return query.OrderBy(m => m.Make).ToArray();
        }
    }
}