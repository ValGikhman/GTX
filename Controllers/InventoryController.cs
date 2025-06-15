using GTX.Models;
using GTX.Session;
using Services;
using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Mvc;
using System.Web.Script.Serialization;

namespace GTX.Controllers {

    public class InventoryController : BaseController {

        public InventoryController(ISessionData sessionData, ILogService LogService)
            : base(sessionData, LogService) {
            if (Model == null) {
                Model = new BaseModel();
                Model.Inventory = new Inventory();
            }
        }

        public ActionResult Index(BaseModel model) {
            ViewBag.Title = $"{Model.Inventory.Title} inventory ({Model.Inventory.Vehicles.Length}) vehicles";
            Log($"{Model.Inventory.Title} inventory");


            return View(Model);
        }

        public ActionResult All() {
            Model.Inventory.Vehicles = SessionData.Inventory.All;
            Model.Inventory.Title = "All";

            return RedirectToAction("Index", Model);
        }

        public ActionResult Suvs() {
            Model.Inventory.Vehicles = SessionData.Inventory.Suvs;
            return RedirectToAction("Index", Model);
        }

        public ActionResult Cars() {
            Model.Inventory.Vehicles = SessionData.Inventory.Cars;
            Model.Inventory.Title = "Cars";
            return RedirectToAction("Index", Model);
        }

        public ActionResult Trucks() {
            Model.Inventory.Vehicles = SessionData.Inventory.Trucks;
            Model.Inventory.Title = "Trucks";

            return RedirectToAction("Index", Model.Inventory.Trucks);
        }

        public ActionResult Vans() {
            Model.Inventory.Vehicles = SessionData.Inventory.Vans;
            Model.Inventory.Title = "Vans";

            return RedirectToAction("Index", Model.Inventory.Vans);
        }

        public ActionResult Cargo() {
            Model.Inventory.Vehicles = SessionData.Inventory.Cargo;
            Model.Inventory.Title = "Cargo";

            return RedirectToAction("Index", Model.Inventory.Cargo);
        }


        [HttpPost]
        public JsonResult ApplyFilter(Filters model) {
            Log($"Applying filter: {SerializeModel(model)}");
            Model.CurrentFilter = model;
            Model.Inventory.Vehicles = ApplyFilters(model);
            Model.Inventory.Title = "Search";
            return Json(new { redirectUrl = Url.Action("Index") });
        }

        [HttpPost]
        public JsonResult ResetFilter(Filters model) {
            SessionData.Inventory.All = null;
            ViewBag.Title = $"Inventory ({SessionData.Inventory.All.Length}) vehicles";

            return Json(new { redirectUrl = Url.Action("All") });
        }

        [HttpGet]
        public JsonResult GetMakes() {
            try {
                return Json(SessionData?.Filters?.Makes, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex) {
                base.Log(ex);
            }
            finally {
            }
            return null;
        }

        [HttpGet]
        public JsonResult GetModels(string makes) {
            try {
                if (!string.IsNullOrEmpty(makes)) {
                    string[] request = new JavaScriptSerializer().Deserialize<string[]>(makes);
                    return Json(SessionData?.Inventory.All?.Where(m => request.Contains(m.Make)).Select(m => m.Model).Distinct().OrderBy(m => m).ToArray(), JsonRequestBehavior.AllowGet);
                }
                else {
                    return Json(SessionData?.Filters?.Models, JsonRequestBehavior.AllowGet);
                }
            }
            catch (Exception ex) {
                base.Log(ex);
            }
            finally {
            }
            return null;
        }

        [HttpGet]
        public JsonResult GetEngines(string makes) {
            try {
                if (!string.IsNullOrEmpty(makes)) {
                    string[] request = new JavaScriptSerializer().Deserialize<string[]>(makes);
                    return Json(SessionData?.Inventory.All?.Where(m => request.Contains(m.Make)).Select(m => m.Engine).Distinct().OrderBy(m => m).ToArray(), JsonRequestBehavior.AllowGet);
                }
                else {
                    return Json(SessionData?.Filters?.Engines, JsonRequestBehavior.AllowGet);
                }
            }
            catch (Exception ex) {
                base.Log(ex);
            }
            finally {
            }
            return null;
        }

        [HttpGet]
        public JsonResult GetFuelTypes(string makes) {
            try {
                if (!string.IsNullOrEmpty(makes)) {
                    string[] request = new JavaScriptSerializer().Deserialize<string[]>(makes);
                    return Json(SessionData?.Inventory.All?.Where(m => request.Contains(m.Make)).Select(m => m.FuelType).Distinct().OrderBy(m => m).ToArray(), JsonRequestBehavior.AllowGet);
                }
                else {
                    return Json(SessionData?.Filters?.FuelTypes, JsonRequestBehavior.AllowGet);
                }
            }
            catch (Exception ex) {
                base.Log(ex);
            }
            finally {
            }
            return null;
        }

        [HttpGet]
        public JsonResult GetDrives(string makes) {
            try {
                if (!string.IsNullOrEmpty(makes)) {
                    string[] request = new JavaScriptSerializer().Deserialize<string[]>(makes);
                    return Json(SessionData?.Inventory.All?.Where(m => request.Contains(m.Make)).Select(m => m.DriveTrain).Distinct().OrderBy(m => m).ToArray(), JsonRequestBehavior.AllowGet);
                }
                else {
                    return Json(SessionData?.Filters?.DriveTrains, JsonRequestBehavior.AllowGet);
                }
            }
            catch (Exception ex) {
                base.Log(ex);
            }
            finally {
            }
            return null;
        }

        [HttpGet]
        public JsonResult GetBodyTypes(string makes) {
            try {
                if (!string.IsNullOrEmpty(makes)) {
                    string[] request = new JavaScriptSerializer().Deserialize<string[]>(makes);
                    return Json(SessionData?.Inventory.All?.Where(m => request.Contains(m.Make)).Select(m => m.Body).Distinct().OrderBy(m => m).ToArray(), JsonRequestBehavior.AllowGet);
                }
                else {
                    return Json(SessionData?.Filters?.BodyTypes, JsonRequestBehavior.AllowGet);
                }
            }
            catch (Exception ex) {
                base.Log(ex);
            }
            finally {
            }
            return null;
        }

        [HttpGet]
        public JsonResult GetPriceRange(string makes) {
            try {
                int? priceMin;
                int? priceMax;
                if (!string.IsNullOrEmpty(makes)) {
                    string[] request = new JavaScriptSerializer().Deserialize<string[]>(makes);
                    priceMax = Model.Inventory.All?.Where(m => request.Contains(m.Make)).Max(m => m.RetailPrice);
                    priceMin = Model.Inventory.All?.Where(m => request.Contains(m.Make)).Min(m => m.RetailPrice);
                }
                else {
                    priceMax = Model.Inventory.All?.Max(m => m.RetailPrice);
                    priceMin = Model.Inventory.All?.Min(m => m.RetailPrice);
                }
                return Json(new { PriceMax = priceMax, PriceMin = priceMin }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex) {
                base.Log(ex);
            }
            finally {
            }
            return null;
        }

        [HttpGet]
        public JsonResult GetMilegeRange(string makes) {
            try {
                int? milesMin;
                int? milesMax;
                if (!string.IsNullOrEmpty(makes)) {
                    string[] request = new JavaScriptSerializer().Deserialize<string[]>(makes);
                    milesMax = SessionData?.Inventory.All?.Where(m => request.Contains(m.Make)).Max(m => m.Mileage);
                    milesMin = SessionData?.Inventory.All?.Where(m => request.Contains(m.Make)).Min(m => m.Mileage);
                }
                else {
                    milesMax = SessionData?.Inventory.All?.Max(m => m.Mileage);
                    milesMin = SessionData?.Inventory.All?.Min(m => m.Mileage);
                }
                return Json(new { MilesMax = milesMax, MilesMin = milesMin }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex) {
                base.Log(ex);
            }
            finally {
            }
            return null;
        }

        private Models.GTX[] ApplyFilters(Filters filter) {
            Models.GTX[] query = Model.Inventory.All;

            if (query.Any() && filter.Makes != null) {
                query = query.Where(m => filter.Makes.Contains(m.Make)).Distinct().ToArray();
            }

            if (query.Any() && filter.Models != null) {
                query = query.Where(m => filter.Models.Contains(m.Model)).Distinct().ToArray();
            }

            if (query.Any() && filter.Engines != null) {
                query = query.Where(m => filter.Engines.Contains(m.Engine)).Distinct().ToArray();
            }

            if (query.Any() && filter.DriveTrains != null) {
                query = query.Where(m => filter.DriveTrains.Contains(m.DriveTrain)).Distinct().ToArray();
            }

            if (query.Any() && filter.BodyTypes != null) {
                query = query.Where(m => filter.BodyTypes.Contains(m.Body)).Distinct().ToArray();
            }

            if (query.Any() && filter.MinMilege > 0 && filter.MaxMilege > 0) {
                query = query.Where(m => m.Mileage >= filter.MinMilege && m.Mileage <= filter.MaxMilege).Distinct().ToArray();
            }

            if (query.Any() && filter.MinPrice > 0 && filter.MaxPrice > 0) {
                query = query.Where(m => m.RetailPrice >= filter.MinPrice && m.RetailPrice <= filter.MaxPrice).Distinct().ToArray();
            }

            return query.OrderBy(m => m.Make).ToArray();
        }
    }
}