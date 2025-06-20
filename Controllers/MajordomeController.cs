﻿using GTX.Models;
using Services;
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
        public ActionResult ApplyTerm(string term) {
            Log($"Applying term: {term.Trim().ToUpper()}");
            var query = ApplyTerms(term);
            return View("Logs", query);
        }

        [HttpPost]
        public ActionResult Reset() {
            Model.Inventory.Vehicles = Model.Inventory.All;
            return Json(new { redirectUrl = Url.Action("Logs") });
        }

        private Log[] ApplyTerms(string term) {
            var query = LogService.GetLogs();
            if (query.Any() && term != null) {
                query = query.Where(m => m.LogLevel.ToUpper().Contains(term.ToUpper()) 
                    || m.Message.ToUpper().Contains(term.ToUpper()) 
                    || m.Url.ToUpper().Contains(term.ToUpper()))
                .Distinct().ToArray();
            }

            return query.OrderByDescending(m => m.DateCreated).ToArray();
        }
    }
}