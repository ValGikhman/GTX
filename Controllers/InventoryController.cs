using GTX.Helpers;
using GTX.Models;
using Services;
using System;
using System.Linq;
using System.Web.Mvc;

namespace GTX.Controllers
{

    public class InventoryController : BaseController {

    public InventoryController(ISessionData sessionData, IInventoryService inventoryService, IVinDecoderService vinDecoderService, IEZ360Service _ez360Service, ILogService logService, IEmployeesService employeesService)
            : base(sessionData, inventoryService, vinDecoderService, _ez360Service, logService, employeesService) {
        }

        [HttpGet]
        public ActionResult Index() {
            Model.Inventory.Title = "Found";
            ViewBag.Message = "Inventory";
            ViewBag.Title = I18n.F("Title_TotalVehicles", Model.Inventory.Title.ToUpper(), Model.Inventory.Vehicles.Length);
            Log($"{Model.Inventory.Title} inventory");

            return View(Model);
        }

        [HttpGet]
        public ActionResult DetailsCard(string stock) {
            Model.CurrentVehicle.VehicleDetails = Model.Inventory.All.FirstOrDefault(m => m.Stock == stock);
            ViewBag.Message = "Inventory";

            return View("DetailsCard", Model.CurrentVehicle.VehicleDetails);
        }

        [HttpGet]
        public ActionResult ContactModal() {
            ViewBag.Message = "Inventory";

            return PartialView("_ContactForm", new GTX.Models.ContactModel());
        }

        [HttpGet]
        public ActionResult TestDriveModal() {
            ViewBag.Message = "Inventory";

            return PartialView("_ContactForm", new GTX.Models.ContactModel(true));
        }

        [HttpGet]
        public ActionResult ApplicationModal() {
            ViewBag.Message = "Inventory";

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

            ViewBag.Message = "Inventory";

            Model.Inventory.Title = I18n.R("All_Details");

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
            Model.CurrentVehicle.VehicleSuggesion = Model.Inventory.All?.Where(m => m.Stock != stock 
                            && m.VehicleType == vehicle.VehicleType 
                            && Math.Abs(m.InternetPrice - vehicle.InternetPrice) < 3000) 
                .Take(10)
                .ToArray() ?? Array.Empty<Models.GTX>();

            // Lets show the tile from fraser details
            ViewBag.Title = $"{vehicle.Year} - {vehicle.Make} - {vehicle.Model}";
            ViewBag.Price = $"{vehicle.InternetPrice.ToString("C", new System.Globalization.CultureInfo("en-US"))}";

            if (fromQR != null && fromQR == vehicle.VIN) {
                return View("DetailsQR", Model);
            }

            return View("Details", Model);
        }

        [HttpGet]
        public ActionResult ShareVehicle(string stock) {
            ViewBag.Message = "Inventory";
            Models.GTX model = Model.Inventory.All.FirstOrDefault(m => m.Stock == stock);
            return PartialView("_AdCard", model);
        }

        [HttpGet]
        public ActionResult All() {
            Model.Inventory.Vehicles = Model?.Inventory?.All ?? Array.Empty<Models.GTX>(); ;
            Model.Inventory.Title = "All";
            ViewBag.Message = "Inventory";

            ViewBag.Title = I18n.F("Title_AllVehicles", Model.Inventory.Vehicles.Length);
            return View("Index", Model);
        }

        [HttpGet]
        public ActionResult Suvs() {
            string body = CommonUnit.VehicleType.SUV.ToString();
            Model.Inventory.Suvs = GetOrEmpty(Model.Categories, body, Array.Empty<Models.GTX>());
            Model.Inventory.Vehicles = Model?.Inventory?.Suvs ?? Array.Empty<Models.GTX>();

            Model.Inventory.Title = I18n.R("Nav_SUVs");
            ViewBag.Title = I18n.F("Title_Category", Model.Inventory.Vehicles.Length, Model.Inventory.Title);
            ViewBag.Message = "Inventory";
            return View("Index", Model);
        }

        [HttpGet]
        public ActionResult Sedans() {
            string body = CommonUnit.VehicleType.SEDAN.ToString();
            Model.Inventory.Sedans = GetOrEmpty(Model.Categories, body, Array.Empty<Models.GTX>());
            Model.Inventory.Vehicles = Model?.Inventory?.Sedans ?? Array.Empty<Models.GTX>();

            Model.Inventory.Title = I18n.R("Nav_Sedans");
            ViewBag.Title = I18n.F("Title_Category", Model.Inventory.Vehicles.Length, Model.Inventory.Title);
            ViewBag.Message = "Inventory";
            return View("Index", Model);
        }

        [HttpGet]
        public ActionResult Wagons() {
            string body = CommonUnit.VehicleType.WAGON.ToString();
            Model.Inventory.Wagons = GetOrEmpty(Model.Categories, body, Array.Empty<Models.GTX>());
            Model.Inventory.Vehicles = Model?.Inventory?.Wagons ?? Array.Empty<Models.GTX>();

            Model.Inventory.Title = I18n.R("Nav_Wagons");
            ViewBag.Title = I18n.F("Title_Category", Model.Inventory.Vehicles.Length, Model.Inventory.Title);
            ViewBag.Message = "Inventory";
            return View("Index", Model);
        }

        [HttpGet]
        public ActionResult Trucks() {
            string body = CommonUnit.VehicleType.TRUCK.ToString();
            Model.Inventory.Trucks = GetOrEmpty(Model.Categories, body, Array.Empty<Models.GTX>());
            Model.Inventory.Vehicles = Model?.Inventory?.Trucks ?? Array.Empty<Models.GTX>();

            Model.Inventory.Title = I18n.R("Nav_Trucks");
            ViewBag.Title = I18n.F("Title_Category", Model.Inventory.Vehicles.Length, Model.Inventory.Title);
            ViewBag.Message = "Inventory";
            return View("Index", Model);
        }

        [HttpGet]
        public ActionResult Vans() {
            string body = CommonUnit.VehicleType.VAN.ToString();
            Model.Inventory.Vans = GetOrEmpty(Model.Categories, body, Array.Empty<Models.GTX>());
            Model.Inventory.Vehicles = Model?.Inventory?.Vans ?? Array.Empty<Models.GTX>();

            Model.Inventory.Title = I18n.R("Nav_Vans");
            ViewBag.Title = I18n.F("Title_Category", Model.Inventory.Vehicles.Length, Model.Inventory.Title);
            ViewBag.Message = "Inventory";
            return View("Index", Model);
        }

        [HttpGet]
        public ActionResult Convertibles() {
            string body = CommonUnit.VehicleType.CONVERTIBLE.ToString();
            Model.Inventory.Convertibles = GetOrEmpty(Model.Categories, body, Array.Empty<Models.GTX>());
            Model.Inventory.Vehicles = Model?.Inventory?.Convertibles ?? Array.Empty<Models.GTX>();

            Model.Inventory.Title = I18n.R("Nav_Convertibles");
            ViewBag.Title = I18n.F("Title_Category", Model.Inventory.Vehicles.Length, Model.Inventory.Title);
            ViewBag.Message = "Inventory";
            return View("Index", Model);
        }

        [HttpGet]
        public ActionResult Hatchbacks() {
            string body = CommonUnit.VehicleType.HATCHBACK.ToString();
            Model.Inventory.Hatchbacks = GetOrEmpty(Model.Categories, body, Array.Empty<Models.GTX>());
            Model.Inventory.Vehicles = Model?.Inventory?.Hatchbacks ?? Array.Empty<Models.GTX>();

            Model.Inventory.Title = I18n.R("Nav_Hatchbacks");
            ViewBag.Title = I18n.F("Title_Category", Model.Inventory.Vehicles.Length, Model.Inventory.Title);
            ViewBag.Message = "Inventory";
            return View("Index", Model);
        }

        [HttpGet]
    public ActionResult Coupes() {
            string body = CommonUnit.VehicleType.COUPE.ToString();
            Model.Inventory.Coupe = GetOrEmpty(Model.Categories, body, Array.Empty<Models.GTX>());
            Model.Inventory.Vehicles = Model?.Inventory?.Coupe ?? Array.Empty<Models.GTX>();

            Model.Inventory.Title = I18n.R("Nav_Coupes");
            ViewBag.Title = I18n.F("Title_Category", Model.Inventory.Vehicles.Length, Model.Inventory.Title);
            ViewBag.Message = "Inventory";
            return View("Index", Model);
        }

        [HttpPost]
        public JsonResult ApplyFilter(Filters model) {
            if (model == null) return Json(new { error = "Model was null" });

            if (model.Transmissions != null) model.Transmissions = model.Transmissions.Select(word => word.Substring(0, 1).ToUpper()).ToArray();

            Model.Inventory.Vehicles = ApplyFilters(model);
            Model.Inventory.Title = I18n.R("All_Search");
            ViewBag.Message = "Inventory";
            return Json(new { redirectUrl = Url.Action("Index") });
        }

        [HttpPost]
        public JsonResult ApplyMakes(string[] make)
        {
            if (make == null) return Json(new { error = "Model was null" });
            var request = new QueryHelper<Models.GTX>(Model.Inventory.All);

            request.InList(m => m.Make, make);
            Model.Inventory.Vehicles = request.Query.OrderBy(m => m.Make).ThenBy(m => m.Model).ToArray();
            Model.Inventory.Title = I18n.R("All_Search");
            ViewBag.Message = "Inventory";
            return Json(new { redirectUrl = Url.Action("Index") });
        }

        [HttpPost]
        public JsonResult ApplyTerm(string term) {
            term = term.Trim().ToUpper();

            Model.Inventory.Vehicles = ApplyTerms(term);
            Model.Inventory.Title = I18n.R("All_Search");
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
        public JsonResult GetModels(string[] makes) {
            try {
                if (makes != null && makes.Length > 0)
                {
                    var rs = Model?.Inventory.All?.Where(m => makes.Contains(m.Make));

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
        public JsonResult GetCylinders(string[] makes) {
            try {
                if (makes != null && makes.Length > 0)
                {
                    var rs = Model?.Inventory.All?.Where(m => makes.Contains(m.Make) && m.Cylinders > 0);
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
        public JsonResult GetTransmissions(string[] makes) {
            try {
                if (makes != null && makes.Length > 0)
                {
                    var rs = Model?.Inventory.All?.Where(m => makes.Contains(m.Make));
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
        public JsonResult GetFuelTypes(string[] makes) {
            try {
                if (makes != null && makes.Length > 0)
                {
                    var rs = Model?.Inventory.All?.Where(m => makes.Contains(m.Make));

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
        public JsonResult GetVehicleTypes(string[] makes) {
            try {
                if (makes != null && makes.Length > 0)
                {
                    var rs = Model?.Inventory.All?.Where(m => makes.Contains(m.Make));

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
        public JsonResult GetDrives(string[] makes) {
            try {
                if (makes != null && makes.Length > 0)
                {
                    var rs = Model?.Inventory.All?.Where(m => makes.Contains(m.Make));

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
        public JsonResult GetBodyTypes(string[] makes) {
            try {
                if (makes != null && makes.Length > 0)
                {
                    var rs = Model?.Inventory.All?.Where(m => makes.Contains(m.Make));

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
        public JsonResult GetPriceRange(string[] makes) {
            try {
                int? priceMin;
                int? priceMax;
                if (makes != null && makes.Length > 0)
                {

                    var rs = Model?.Inventory.All?.Where(m => makes.Contains(m.Make));

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
        public JsonResult GetMilegeRange(string[] makes) {
            try {
                int? milesMin;
                int? milesMax;
                if (makes != null && makes.Length > 0)
                {

                    var rs = Model?.Inventory.All?.Where(m => makes.Contains(m.Make));

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
    }
}