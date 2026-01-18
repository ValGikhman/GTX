using GTX.Models;
using Services;
using System;
using System.Linq;
using System.Web.Mvc;
using System.Web.Script.Serialization;

namespace GTX.Controllers
{

    public class InventoryController : BaseController {
        public InventoryController(ISessionData sessionData, IInventoryService inventoryService, IVinDecoderService vinDecoderService, IEZ360Service _ez360Service, ILogService logService, IBlogPostService blogPostService)
            : base(sessionData, inventoryService, vinDecoderService, _ez360Service, logService, blogPostService) {

        }

        [HttpGet]
        public ActionResult Index() {

            Model.Inventory.Title = "Found";
            ViewBag.Title = $"{Model.Inventory.Title.ToUpper()} { Model.Inventory.Vehicles.Length} vehicle(s)";

            Log($"{Model.Inventory.Title} inventory");
            return View(Model);
        }

        [HttpGet]
        public ActionResult DetailsCard(string stock) {
            Model.CurrentVehicle.VehicleDetails = Model.Inventory.All.FirstOrDefault(m => m.Stock == stock);

            return View("DetailsCard", Model.CurrentVehicle.VehicleDetails);
        }

        [HttpGet]
        public ActionResult ContactModal() {
            return PartialView("_ContactForm", new GTX.Models.ContactModel());
        }

        [HttpGet]
        public ActionResult TestDriveModal() {
            return PartialView("_ContactForm", new GTX.Models.ContactModel(true));
        }

        [HttpGet]
        public ActionResult ApplicationModal() {
            return PartialView("_LoanApplication");
        }

        [HttpGet]
        public ActionResult Details(string stock) {
            var fromQR = Request.QueryString["QR"];

            stock = stock?.Trim().ToUpper();

            if (string.IsNullOrEmpty(stock)) {
                Model.Inventory.Title = "All";
                Model.Inventory.Vehicles = Model?.Inventory?.All as Models.GTX[] ?? Array.Empty<Models.GTX>();
                ViewBag.Title = $"{Model.Inventory.Vehicles.Length} vehicles";

                return View("Index", Model);
            }

            Model.Inventory.Title = "Details";

            var vehicle = Model.Inventory.All?.FirstOrDefault(m => m.Stock == stock);
            if (vehicle == null) {
                return HttpNotFound($"Vehicle with stock '{stock}' not found.");
            }

            Model.CurrentVehicle.VehicleDetails = vehicle;
            Model.CurrentVehicle.VehicleDetails.Story = vehicle.Story;

            // If there is no DataOne get it
            if (Model.IsDataOne)
            {
                if (vehicle.DataOne == null)
                {
                    var details = VinDecoderService.DecodeVin(vehicle.VIN, dataOneApiKey, dataOneSecretApiKey);
                    InventoryService.SaveDataOneDetails(stock, details);
                    Model.CurrentVehicle.VehicleDataOneDetails = GetDecodedData(stock);
                }
                else {
                    Model.CurrentVehicle.VehicleDataOneDetails = vehicle.DataOne;
                }
            }

            if (Model.IsEZ360)
            {
                Model.CurrentVehicle.DisplayEZ360Player = false;
                var ez360 = vehicle.EZ360;
                if (ez360 != null)
                {
                    Model.CurrentVehicle.DisplayEZ360Player = (ez360.DetailPics.Length > 0 && ez360.IsPublishable);
                }
            }

            // Suggest similar vehicles (within $3000 range, excluding the current one)
            Model.CurrentVehicle.VehicleSuggesion = Model.Inventory.All?.Where(m => m.Stock != stock && m.VehicleType == vehicle.VehicleType 
                            && Math.Abs(m.InternetPrice - vehicle.InternetPrice) < 3000) 
                .Take(10)
                .ToArray() ?? Array.Empty<Models.GTX>();

            // Lets show the tile from fraser details
            ViewBag.Title = $"{vehicle.Year} - {vehicle.Make} - {vehicle.Model} {vehicle.VehicleStyle}";
            ViewBag.Price = $"{vehicle.InternetPrice.ToString("C")}";

            if (fromQR != null && fromQR == vehicle.VIN) {
                return View("DetailsQR", Model);
            }

            return View("Details", Model);
        }

        [HttpGet]
        public ActionResult ShareVehicle(string stock) {
            Models.GTX model = Model.Inventory.All.FirstOrDefault(m => m.Stock == stock);
            return PartialView("_AdCard", model);
        }

        [HttpGet]
        public ActionResult All() {
            Model.Inventory.Vehicles = Model.Inventory?.All ?? Array.Empty<Models.GTX>();

            Model.Inventory.Title = "All";
            ViewBag.Title = $"{Model.Inventory.Vehicles.Length} vehicles";

            return View("Index", Model);
        }

        [HttpGet]
        public ActionResult Suvs() {
            if (Model.Inventory.Suvs == null) {
                string body = CommonUnit.VehicleType.SUV.ToString();
                Model.Inventory.Suvs = GetOrEmpty(Model.Categories, body, Array.Empty<Models.GTX>());
            }

            Model.Inventory.Vehicles = Model.Inventory?.Suvs ?? Array.Empty<Models.GTX>();
            Model.Inventory.Title = "Suv(s)";
            ViewBag.Title = $"{Model.Inventory.Vehicles.Length} {Model.Inventory.Title.ToUpper()}";

            return View("Index", Model);
        }

        [HttpGet]
        public ActionResult Sedans() {
            if (Model.Inventory.Sedans == null) {
                string body = CommonUnit.VehicleType.SEDAN.ToString();
                Model.Inventory.Sedans = GetOrEmpty(Model.Categories, body, Array.Empty<Models.GTX>());
            }
            
            Model.Inventory.Vehicles = Model?.Inventory?.Sedans ?? Array.Empty<Models.GTX>();
            Model.Inventory.Title = "Sedan(s)";
            ViewBag.Title = $"{Model.Inventory.Vehicles.Length} {Model.Inventory.Title.ToUpper()}";

            return View("Index", Model);
        }

        [HttpGet]
        public ActionResult Wagons() {
            if (Model?.Inventory?.Wagons == null)
            {
                string body = CommonUnit.VehicleType.WAGON.ToString();
                Model.Inventory.Wagons = GetOrEmpty(Model.Categories, body, Array.Empty<Models.GTX>());
            }

            Model.Inventory.Vehicles = Model?.Inventory?.Wagons ?? Array.Empty<Models.GTX>();
            Model.Inventory.Title = "Wagon(s)";
            ViewBag.Title = $"{Model.Inventory.Vehicles.Length} {Model.Inventory.Title.ToUpper()}";

            return View("Index", Model);
        }

        [HttpGet]
        public ActionResult Trucks() {
            if (Model?.Inventory?.Trucks == null)
            {
                string body = CommonUnit.VehicleType.TRUCK.ToString();
                Model.Inventory.Trucks = GetOrEmpty(Model.Categories, body, Array.Empty<Models.GTX>());
            }

            Model.Inventory.Vehicles = Model?.Inventory?.Trucks ?? Array.Empty<Models.GTX>();
            Model.Inventory.Title = "Truck(s)";
            ViewBag.Title = $"{Model.Inventory.Vehicles.Length} {Model.Inventory.Title.ToUpper()}";

            return View("Index", Model);
        }

        [HttpGet]
        public ActionResult Vans() {
            if (Model?.Inventory?.Vans == null)
            {
                string body = CommonUnit.VehicleType.VAN.ToString();
                Model.Inventory.Vans = GetOrEmpty(Model.Categories, body, Array.Empty<Models.GTX>());
            }

            Model.Inventory.Vehicles = Model?.Inventory?.Vans ?? Array.Empty<Models.GTX>();
            Model.Inventory.Title = "Van(s)";
            ViewBag.Title = $"{Model.Inventory.Vehicles.Length} {Model.Inventory.Title.ToUpper()}";

            return View("Index", Model);
        }

        [HttpGet]
        public ActionResult Convertibles() {
            if (Model?.Inventory?.Convertibles == null)
            {
                string body = CommonUnit.VehicleType.CONVERTIBLE.ToString();
                Model.Inventory.Convertibles = GetOrEmpty(Model.Categories, body, Array.Empty<Models.GTX>());
            }

            Model.Inventory.Vehicles = Model?.Inventory?.Convertibles ?? Array.Empty<Models.GTX>();
            Model.Inventory.Title = "Convertible(s)";
            ViewBag.Title = $"{Model.Inventory.Vehicles.Length} {Model.Inventory.Title.ToUpper()}";

            return View("Index", Model);
        }

        [HttpGet]
        public ActionResult Hatchbacks() {
            if (Model?.Inventory?.Hatchbacks == null)
            {
                string body = CommonUnit.VehicleType.HATCHBACK.ToString();
                Model.Inventory.Hatchbacks = GetOrEmpty(Model.Categories, body, Array.Empty<Models.GTX>());
            }

            Model.Inventory.Vehicles = Model?.Inventory?.Hatchbacks ?? Array.Empty<Models.GTX>();
            Model.Inventory.Title = "Hatchback(s)";
            ViewBag.Title = $"{Model.Inventory.Vehicles.Length} {Model.Inventory.Title.ToUpper()}";

            return View("Index", Model);
        }

        [HttpGet]
        public ActionResult Coupes() {
            if (Model?.Inventory?.Coupe == null)
            {
                string body = CommonUnit.VehicleType.COUPE.ToString();
                Model.Inventory.Coupe = GetOrEmpty(Model.Categories, body, Array.Empty<Models.GTX>());
            }

            Model.Inventory.Vehicles = Model?.Inventory?.Coupe ?? Array.Empty<Models.GTX>();
            Model.Inventory.Title = "Coupe(s)";
            ViewBag.Title = $"{Model.Inventory.Vehicles.Length} {Model.Inventory.Title.ToUpper()}";

            return View("Index", Model);
        }

        [HttpPost]
        public JsonResult ApplyFilter(Filters model) {
            Log($"Applying filter: {SerializeModel(model)}");

            if (model.Transmissions != null) {
                model.Transmissions = model.Transmissions.Select(word => word.Substring(0, 1).ToUpper()).ToArray();
            }

            var filteredVehicles = ApplyFilters(model);

            Model.Inventory.Vehicles = filteredVehicles;
            Model.Inventory.Title = "Search";

            return Json(new { redirectUrl = Url.Action("Index") });
        }

        [HttpPost]
        public JsonResult ApplyTerm(string term) {
            Log($"Applying term: {term}");

            term = term.Trim().ToUpper();

            Model.Inventory.Vehicles = ApplyTerms(term);
            Model.Inventory.Title = "Search";

            return Json(new { redirectUrl = Url.Action("Index") });
        }

        [HttpGet]
        [AllowAnonymous]
        public JsonResult GetMakes() {
            try {
                return Json(Model?.Filters?.Makes, JsonRequestBehavior.AllowGet);
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
                return Json(Model?.Filters?.Makes, JsonRequestBehavior.AllowGet);
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
                    var rs = Model?.Inventory.All?.Where(m => request.Contains(m.Make));

                    return Json(rs.Select(m => m.Model).Distinct().OrderBy(m => m).ToArray(), JsonRequestBehavior.AllowGet);
                }
                else {
                    return Json(Model?.Filters?.Models, JsonRequestBehavior.AllowGet);
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
        public JsonResult GetCylinders(string makes) {
            try {
                if (!string.IsNullOrEmpty(makes)) {
                    string[] request = new JavaScriptSerializer().Deserialize<string[]>(makes);
                    var rs = Model?.Inventory.All?.Where(m => request.Contains(m.Make) && m.Cylinders > 0);

                    return Json(rs.Select(m => m.Cylinders).Distinct().OrderBy(m => m).ToArray(), JsonRequestBehavior.AllowGet);
                }
                else {
                    return Json(Model?.Filters?.Cylinders, JsonRequestBehavior.AllowGet);
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
        public JsonResult GetTransmissions(string makes) {
            try {
                if (!string.IsNullOrEmpty(makes)) {
                    string[] request = new JavaScriptSerializer().Deserialize<string[]>(makes);
                    var rs = Model?.Inventory.All?.Where(m => request.Contains(m.Make));

                    return Json(rs.Select(m => Models.GTX.WordIt(m.Transmission)).Distinct().OrderBy(m => m).ToArray(), JsonRequestBehavior.AllowGet);
                }
                else {
                    return Json(Model?.Filters?.Transmissions, JsonRequestBehavior.AllowGet);
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
                    var rs = Model?.Inventory.All?.Where(m => request.Contains(m.Make));

                    return Json(rs.Select(m => m.FuelType).Distinct().OrderBy(m => m).ToArray(), JsonRequestBehavior.AllowGet);
                }
                else {
                    return Json(Model?.Filters?.FuelTypes, JsonRequestBehavior.AllowGet);
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
                    var rs = Model?.Inventory.All?.Where(m => request.Contains(m.Make));

                    return Json(rs.Select(m => m.VehicleType).Distinct().OrderBy(m => m).ToArray(), JsonRequestBehavior.AllowGet);
                }
                else {
                    return Json(Model?.Filters?.VehicleTypes, JsonRequestBehavior.AllowGet);
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
                    var rs = Model?.Inventory.All?.Where(m => request.Contains(m.Make));

                    return Json(rs.Select(m => m.DriveTrain).Distinct().OrderBy(m => m).ToArray(), JsonRequestBehavior.AllowGet);
                }
                else {
                    return Json(Model?.Filters?.DriveTrains, JsonRequestBehavior.AllowGet);
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
                    var rs = Model?.Inventory.All?.Where(m => request.Contains(m.Make));

                    return Json(rs.Select(m => m.Body).Distinct().OrderBy(m => m).ToArray(), JsonRequestBehavior.AllowGet);
                }
                else {
                    return Json(Model?.Filters?.BodyTypes, JsonRequestBehavior.AllowGet);
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
                    var rs = Model?.Inventory.All?.Where(m => request.Contains(m.Make));

                    priceMax = rs?.Max(m => m.InternetPrice);
                    priceMin = rs?.Min(m => m.InternetPrice);
                }
                else {
                    priceMax = Model?.Inventory?.All?.Max(m => m.InternetPrice);
                    priceMin = Model?.Inventory?.All?.Min(m => m.InternetPrice);
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
                    var rs = Model?.Inventory.All?.Where(m => request.Contains(m.Make));

                    milesMax = rs?.Max(m => m.Mileage);
                    milesMin = rs?.Min(m => m.Mileage);
                }
                else {
                    milesMax = Model?.Inventory?.All?.Max(m => m.Mileage);
                    milesMin = Model?.Inventory?.All?.Min(m => m.Mileage);
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

            if (query.Any() && filter.Cylinders != null) {
                query = query.Where(m => filter.Cylinders.Contains(m.Cylinders.ToString())).Distinct().ToArray();
            }

            if (query.Any() && filter.Transmissions != null) {
                query = query.Where(m => filter.Transmissions.Contains(m.Transmission)).Distinct().ToArray();
            }

            if (query.Any() && filter.DriveTrains != null) {
                query = query.Where(m => filter.DriveTrains.Contains(m.DriveTrain)).Distinct().ToArray();
            }

            if (query.Any() && filter.BodyTypes != null) {
                query = query.Where(m => filter.BodyTypes.Contains(m.Body)).Distinct().ToArray();
            }

            if (query.Any() && filter.MinMilege >= 0 && filter.MaxMilege > 0) {
                query = query.Where(m => m.Mileage >= filter.MinMilege && m.Mileage <= filter.MaxMilege).Distinct().ToArray();
            }

            if (query.Any() && filter.MinPrice >= 0 && filter.MaxPrice > 0) {
                query = query.Where(m => m.InternetPrice >= filter.MinPrice && m.InternetPrice <= filter.MaxPrice).Distinct().ToArray();
            }

            if (query.Any() && filter.FuelTypes != null) {
                query = query.Where(m => filter.FuelTypes.Contains(m.FuelType)).Distinct().ToArray();
            }

            if (query.Any() && filter.VehicleTypes != null) {
                query = query.Where(m => filter.VehicleTypes.Contains(m.VehicleType)).Distinct().ToArray();
            }

            return query.OrderBy(m => m.Make).ThenBy(m => m.Model).ToArray();
        }
    }
}