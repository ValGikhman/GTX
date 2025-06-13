using GTX.Models;
using GTX.Session;
using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Mvc;
using System.Web.Script.Serialization;

namespace GTX.Controllers {

    public class InventoryController : BaseController {

        public InventoryController(ISessionData sessionData)
            : base(sessionData) {}

        [HttpGet]
        public async Task<ActionResult> All() {
            ViewBag.Message = "Inventory";

            Vehicle[] model = await SetModel();

            ViewBag.Title = $"Inventory ({model.Length}) vehicles";
            return View(model);
        }

        [HttpPost]
        public ActionResult ApplyFilter(Filters filters) {
            SessionData.Vehicles = SessionData.Vehicles.Where(m => m.Make.ToUpper() == "Honda".ToUpper()).ToArray();
            ViewBag.Title = $"Inventory ({SessionData.Vehicles.Length}) vehicles";

            return RedirectToAction("All");
        }

        private async Task<Vehicle[]> SetModel() {
            if (SessionData?.Vehicles == null) {
                Vehicle[] model;
                model = await Utility.XMLHelpers.XmlRepository.GetInventory();
                model = model.Where(m => m.RetailPrice > 0).ToArray();
                SessionData.SetSession(Constants.SESSION_INVENTORY, model);
                return model;
            }

            return SessionData.Vehicles;
        }

        private async Task<string> DecodeVin(string vin) {
            using (HttpClient client = new HttpClient()) {
                string url = $"https://vpic.nhtsa.dot.gov/api/vehicles/decodevin/{vin}?format=xml";
                HttpResponseMessage response = await client.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                    return "Error fetching VIN data.";

                var data = await response.Content.ReadAsStringAsync();
                return data;
            }
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
                    return Json(SessionData?.Vehicles?.Where(m => request.Contains(m.Make)).Select(m => m.Model).Distinct().OrderBy(m => m).ToArray(), JsonRequestBehavior.AllowGet);
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
                    return Json(SessionData?.Vehicles?.Where(m => request.Contains(m.Make)).Select(m => m.Engine).Distinct().OrderBy(m => m).ToArray(), JsonRequestBehavior.AllowGet);
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
                    return Json(SessionData?.Vehicles?.Where(m => request.Contains(m.Make)).Select(m => m.FuelType).Distinct().OrderBy(m => m).ToArray(), JsonRequestBehavior.AllowGet);
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
                    return Json(SessionData?.Vehicles?.Where(m => request.Contains(m.Make)).Select(m => m.DriveTrain).Distinct().OrderBy(m => m).ToArray(), JsonRequestBehavior.AllowGet);
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
                    return Json(SessionData?.Vehicles?.Where(m => request.Contains(m.Make)).Select(m => m.Body).Distinct().OrderBy(m => m).ToArray(), JsonRequestBehavior.AllowGet);
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
                    priceMax = SessionData?.Vehicles?.Where(m => request.Contains(m.Make)).Max(m => m.RetailPrice);
                    priceMin = SessionData?.Vehicles?.Where(m => request.Contains(m.Make)).Min(m => m.RetailPrice);
                }
                else {
                    priceMax = SessionData?.Vehicles?.Max(m => m.RetailPrice);
                    priceMin = SessionData?.Vehicles?.Min(m => m.RetailPrice);
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
                    milesMax = SessionData?.Vehicles?.Where(m => request.Contains(m.Make)).Max(m => m.Mileage);
                    milesMin = SessionData?.Vehicles?.Where(m => request.Contains(m.Make)).Min(m => m.Mileage);
                }
                else {
                    milesMax = SessionData?.Vehicles?.Max(m => m.Mileage);
                    milesMin = SessionData?.Vehicles?.Min(m => m.Mileage);
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

    }
}