using GTX.Models;
using Newtonsoft.Json;
using Services;
using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web.Mvc;
using System.Web.Script.Serialization;
using System.Xml.Linq;
using System.Xml.Serialization;

namespace GTX.Controllers {

    public class InventoryController : BaseController {
        private readonly HttpClient httpClient = new();
        public InventoryController(ISessionData sessionData, IInventoryService inventoryService, IVinDecoderService vinDecoderService, ILogService LogService)
            : base(sessionData, inventoryService, vinDecoderService, LogService) {
        }

        [HttpGet]
        public ActionResult Index(BaseModel model) {
            var vehicles = Model.Inventory.Vehicles ?? Array.Empty<Models.GTX>();
            Model.Inventory.Title = "Found";
            ViewBag.Title = $"{Model.Inventory.Title.ToUpper()} {vehicles.Length} vehicles";
            Log($"{Model.Inventory.Title} inventory");

            return View(model);
        }

        [HttpGet]
        public ActionResult DetailsCard(string stock) {
            Model.CurrentVehicle.VehicleDetails = Model.Inventory.All.FirstOrDefault(m => m.Stock == stock);

            return View("DetailsCard", Model.CurrentVehicle.VehicleDetails);
        }

        [HttpGet]
        public ActionResult Details(string stock) {
            stock = stock?.Trim().ToUpper();

            if (string.IsNullOrEmpty(stock)) {
                Model.Inventory.Title = "All";
                Model.Inventory.Vehicles = SessionData?.Inventory?.All as Models.GTX[] ?? Array.Empty<Models.GTX>();
                ViewBag.Title = $"{Model.Inventory.Vehicles.Length} vehicles";

                return View("Index", Model);
            }
            Model.Inventory.Title = "Details";

            var vehicle = Model.Inventory.All?.FirstOrDefault(m => m.Stock == stock);
            if (vehicle == null) {
                return HttpNotFound($"Vehicle with stock '{stock}' not found.");
            }

            Model.CurrentVehicle.VehicleDetails = vehicle;
            Model.CurrentVehicle.VehicleDataOneDetails = GetDecodedData(stock);
            Model.CurrentVehicle.VehicleImages = GetImages(stock);

            SessionData.CurrentVehicle = Model.CurrentVehicle;

            // Suggest similar vehicles (within $3000 range, excluding the current one)
            Model.CurrentVehicle.VehicleSuggesion = Model.Inventory.All?
                .Where(m => m.Stock != stock && Math.Abs(m.RetailPrice - vehicle.RetailPrice) < 3000)
                .Take(10)
                .ToArray() ?? Array.Empty<Models.GTX>();

            if (Model.CurrentVehicle.VehicleDataOneDetails == null) {
                ViewBag.Title = $"{vehicle.Year} - {vehicle.Make} - {vehicle.Model} {vehicle.VehicleStyle}";
            }
            else {
                ViewBag.Title = $"{Model.CurrentVehicle.VehicleDataOneDetails.QueryResponses.Items[0].UsMarketData.UsStyles.Styles[0].Name.ToUpper()}";
            }

            return View("Details", Model);
        }

        [HttpGet]
        public ActionResult ShareVehicle(string stock) {
            Models.GTX model = Model.Inventory.All.FirstOrDefault(m => m.Stock == stock);
            return PartialView("_AdCard", model);
        }

        [HttpGet]
        public async Task<ActionResult> GetReport(string vin) {
            string url = $"https://www.carfax.com/VehicleHistory/p/Report.cfx?vin={vin}";

            using (var client = new HttpClient()) {
                try {
                    var html = await client.GetStringAsync(url);
                    return Content(html, "text/html"); // Send raw HTML
                }
                catch (Exception ex) {
                    return Content("Unable to fetch Carfax report.");
                }
            }
        }

        [HttpGet]
        public ActionResult All() {
            var vehicles = SessionData?.Inventory?.All ?? Array.Empty<Models.GTX>();

            Model.Inventory.Vehicles = vehicles;
            Model.Inventory.Title = "All";
            ViewBag.Title = $"{vehicles.Length} vehicles";

            return View("Index", Model);
        }

        [HttpGet]
        public ActionResult Suvs() {
            Model.Inventory.Vehicles = SessionData?.Inventory?.Suvs ?? Array.Empty<Models.GTX>();

            Model.Inventory.Title = "Suv(s)";
            ViewBag.Title = $"{Model.Inventory.Vehicles.Length} {Model.Inventory.Title.ToUpper()}";

            return View("Index", Model);
        }

        [HttpGet]
        public ActionResult Sedans() {
            Model.Inventory.Vehicles = SessionData?.Inventory?.Sedans ?? Array.Empty<Models.GTX>();

            Model.Inventory.Title = "Sedan(s)";
            ViewBag.Title = $"{Model.Inventory.Vehicles.Length} {Model.Inventory.Title.ToUpper()}";

            return View("Index", Model);
        }

        [HttpGet]
        public ActionResult Wagons() {
            Model.Inventory.Vehicles = SessionData?.Inventory?.Wagons ?? Array.Empty<Models.GTX>();

            Model.Inventory.Title = "Wagon(s)";
            ViewBag.Title = $"{Model.Inventory.Vehicles.Length} {Model.Inventory.Title.ToUpper()}";

            return View("Index", Model);
        }

        [HttpGet]
        public ActionResult Trucks() {
            Model.Inventory.Vehicles = SessionData?.Inventory?.Trucks ?? Array.Empty<Models.GTX>();

            Model.Inventory.Title = "Truck(s)";
            ViewBag.Title = $"{Model.Inventory.Vehicles.Length} {Model.Inventory.Title.ToUpper()}";

            return View("Index", Model);
        }

        [HttpGet]
        public ActionResult Vans() {
            Model.Inventory.Vehicles = SessionData?.Inventory?.Vans ?? Array.Empty<Models.GTX>();

            Model.Inventory.Title = "Van(s)";
            ViewBag.Title = $"{Model.Inventory.Vehicles.Length} {Model.Inventory.Title.ToUpper()}";

            return View("Index", Model);
        }

        [HttpGet]
        public ActionResult Cargo() {
            Model.Inventory.Vehicles = SessionData?.Inventory?.Cargo ?? Array.Empty<Models.GTX>();

            Model.Inventory.Title = "Cargo(s)";
            ViewBag.Title = $"{Model.Inventory.Vehicles.Length} {Model.Inventory.Title.ToUpper()}";

            return View("Index", Model);
        }

        [HttpGet]
        public ActionResult Convertibles() {
            Model.Inventory.Vehicles = SessionData?.Inventory?.Convertibles ?? Array.Empty<Models.GTX>();

            Model.Inventory.Title = "Convertible(s)";
            ViewBag.Title = $"{Model.Inventory.Vehicles.Length} {Model.Inventory.Title.ToUpper()}";

            return View("Index", Model);
        }

        [HttpGet]
        public ActionResult Hatchbacks() {
            Model.Inventory.Vehicles = SessionData?.Inventory?.Hatchbacks ?? Array.Empty<Models.GTX>();

            Model.Inventory.Title = "Hatchback(s)";
            ViewBag.Title = $"{Model.Inventory.Vehicles.Length} {Model.Inventory.Title.ToUpper()}";

            return View("Index", Model);
        }

        [HttpGet]
        public ActionResult Coupes() {
            Model.Inventory.Vehicles = SessionData?.Inventory?.Coupe ?? Array.Empty<Models.GTX>();

            Model.Inventory.Title = "Coupe(s)";
            ViewBag.Title = $"{Model.Inventory.Vehicles.Length} {Model.Inventory.Title.ToUpper()}";

            return View("Index", Model);
        }

        [HttpPost]
        public JsonResult ApplyFilter(Filters model) {
            Log($"Applying filter: {SerializeModel(model)}");

            var filteredVehicles = ApplyFilters(model);

            Model.CurrentFilter = model;
            Model.Inventory.Vehicles = filteredVehicles;
            Model.Inventory.Title = "Search";

            return Json(new { redirectUrl = Url.Action("Index") });
        }

        [HttpPost]
        public JsonResult ApplyTerm(string term) {
            Log($"Applying term: {term}");

            term = term.Trim().ToUpper();

            Model.CurrentFilter = null;
            Model.Inventory.Vehicles = ApplyTerms(term);
            Model.Inventory.Title = "Search";
            return Json(new { redirectUrl = Url.Action("Index") });
        }

        [HttpGet]
        [AllowAnonymous]
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
        [AllowAnonymous]
        public JsonResult GetMakesImages() {
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
        [AllowAnonymous]
        public JsonResult GetModels(string makes) {
            try {
                if (!string.IsNullOrEmpty(makes)) {
                    string[] request = new JavaScriptSerializer().Deserialize<string[]>(makes);
                    var rs = SessionData?.Inventory.All?.Where(m => request.Contains(m.Make));

                    return Json(rs.Select(m => m.Model).Distinct().OrderBy(m => m).ToArray(), JsonRequestBehavior.AllowGet);
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
        [AllowAnonymous]
        public JsonResult GetEngines(string makes) {
            try {
                if (!string.IsNullOrEmpty(makes)) {
                    string[] request = new JavaScriptSerializer().Deserialize<string[]>(makes);
                    var rs = SessionData?.Inventory.All?.Where(m => request.Contains(m.Make));

                    return Json(rs.Select(m => m.Engine).Distinct().OrderBy(m => m).ToArray(), JsonRequestBehavior.AllowGet);
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
        [AllowAnonymous]
        public JsonResult GetFuelTypes(string makes) {
            try {
                if (!string.IsNullOrEmpty(makes)) {
                    string[] request = new JavaScriptSerializer().Deserialize<string[]>(makes);
                    var rs = SessionData?.Inventory.All?.Where(m => request.Contains(m.Make));

                    return Json(rs.Select(m => m.FuelType).Distinct().OrderBy(m => m).ToArray(), JsonRequestBehavior.AllowGet);
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
        [AllowAnonymous]
        public JsonResult GetVehicleTypes(string makes) {
            try {
                if (!string.IsNullOrEmpty(makes)) {
                    string[] request = new JavaScriptSerializer().Deserialize<string[]>(makes);
                    var rs = SessionData?.Inventory.All?.Where(m => request.Contains(m.Make));

                    return Json(rs.Select(m => m.VehicleType).Distinct().OrderBy(m => m).ToArray(), JsonRequestBehavior.AllowGet);
                }
                else {
                    return Json(SessionData?.Filters?.VehicleTypes, JsonRequestBehavior.AllowGet);
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
        [AllowAnonymous]
        public JsonResult GetDrives(string makes) {
            try {
                if (!string.IsNullOrEmpty(makes)) {
                    string[] request = new JavaScriptSerializer().Deserialize<string[]>(makes);
                    var rs = SessionData?.Inventory.All?.Where(m => request.Contains(m.Make));

                    return Json(rs.Select(m => m.DriveTrain).Distinct().OrderBy(m => m).ToArray(), JsonRequestBehavior.AllowGet);
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
        [AllowAnonymous]
        public JsonResult GetBodyTypes(string makes) {
            try {
                if (!string.IsNullOrEmpty(makes)) {
                    string[] request = new JavaScriptSerializer().Deserialize<string[]>(makes);
                    var rs = SessionData?.Inventory.All?.Where(m => request.Contains(m.Make));

                    return Json(rs.Select(m => m.Body).Distinct().OrderBy(m => m).ToArray(), JsonRequestBehavior.AllowGet);
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
        [AllowAnonymous]
        public JsonResult GetPriceRange(string makes) {
            try {
                int? priceMin;
                int? priceMax;
                if (!string.IsNullOrEmpty(makes)) {
                    string[] request = new JavaScriptSerializer().Deserialize<string[]>(makes);
                    var rs = SessionData?.Inventory.All?.Where(m => request.Contains(m.Make));

                    priceMax = rs?.Max(m => m.RetailPrice);
                    priceMin = rs?.Min(m => m.RetailPrice);
                }
                else {
                    priceMax = SessionData?.Inventory?.All?.Max(m => m.RetailPrice);
                    priceMin = SessionData?.Inventory?.All?.Min(m => m.RetailPrice);
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
        [AllowAnonymous]
        public JsonResult GetMilegeRange(string makes) {
            try {
                int? milesMin;
                int? milesMax;
                if (!string.IsNullOrEmpty(makes)) {
                    string[] request = new JavaScriptSerializer().Deserialize<string[]>(makes);
                    var rs = SessionData?.Inventory.All?.Where(m => request.Contains(m.Make));

                    milesMax = rs?.Max(m => m.Mileage);
                    milesMin = rs?.Min(m => m.Mileage);
                }
                else {
                    milesMax = SessionData?.Inventory?.All?.Max(m => m.Mileage);
                    milesMin = SessionData?.Inventory?.All?.Min(m => m.Mileage);
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

            if (query.Any() && filter.FuelTypes != null) {
                query = query.Where(m => filter.FuelTypes.Contains(m.FuelType)).Distinct().ToArray();
            }

            if (query.Any() && filter.VehicleTypes != null) {
                query = query.Where(m => filter.VehicleTypes.Contains(m.VehicleType)).Distinct().ToArray();
            }

            return query.OrderBy(m => m.Make).ThenBy(m => m.Model).ToArray();
        }

        private DecodedData GetDecodedData(string stock) {
            string dataOne = InventoryService.GetDataOneDetails(stock);

            var (errCode, errMsg) = ParseDecoderError(dataOne);

            if (errCode != null) {
                return null;
            }

            var serializer = new XmlSerializer(typeof(DecodedData));
            using (TextReader reader = new StringReader(dataOne)) {
                return (DecodedData)serializer.Deserialize(reader);
            }
        }

        private static (string? code, string? message) ParseDecoderError(string xml) {
            try {
                var doc = System.Xml.Linq.XDocument.Parse(xml);
                var err = doc.Descendants("decoder_errors").Descendants("error").FirstOrDefault();
                if (err == null) return (null, null);

                var code = (string?)err.Element("code");
                var msg = (string?)err.Element("message");
                return (code, msg);
            }
            catch {
                // If it isn't valid XML, treat as a body/format error
                return ("PARSE", "Invalid XML from decoder");
            }
        }

    }
}