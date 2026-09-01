using GTX.Common;
using GTX.Helpers;
using GTX.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Services;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.Caching;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.Mvc;

namespace GTX.Controllers
{

    public class InventoryController : BaseController {

        private const int MinimumComparisonVehicles = 2;
        private const int MaximumComparisonVehicles = 4;
        private static readonly HttpClient ComparisonOpenAiClient = CreateComparisonOpenAiClient();
        private static readonly MemoryCache ComparisonCache = MemoryCache.Default;
        private static readonly object ComparisonRateLock = new object();

    public InventoryController(ISessionData sessionData, IInventoryService inventoryService, IVinDecoderService vinDecoderService, ILogService logService, IEmployeesService employeesService)
            : base(sessionData, inventoryService, vinDecoderService, logService, employeesService) {
        }

        [HttpGet]
        public ActionResult Index() {
            Model.Inventory.Title = "Found";
            ViewBag.Message = "Inventory";
            ViewBag.Title = I18n.F("Title_TotalVehicles", Model.Inventory.Title.ToUpper(), Model.Inventory.Vehicles.Length);
            Log($"{Model.Inventory.Title} inventory");

            return View(Model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Compare(string[] stocks) {
            var requestedStocks = (stocks ?? Array.Empty<string>())
                .Where(stock => !string.IsNullOrWhiteSpace(stock))
                .Select(stock => stock.Trim().ToUpperInvariant())
                .Where(stock => Regex.IsMatch(stock, @"^[A-Z0-9_-]{1,20}$"))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (requestedStocks.Length < MinimumComparisonVehicles || requestedStocks.Length > MaximumComparisonVehicles) {
                return ComparisonError(HttpStatusCode.BadRequest, "Select between 2 and 4 distinct vehicles to compare.");
            }

            if (Model.IsDataOne) {
                try {
                    SetModel();
                }
                catch (Exception ex) {
                    // A DataOne storage problem must not prevent the inventory comparison.
                    Log("DataOne comparison inventory load failed: " + ex.Message);
                }
            }

            var publicInventory = Model?.Inventory?.All ?? Array.Empty<Models.GTX>();
            var vehicles = requestedStocks
                .Select(stock => publicInventory.FirstOrDefault(vehicle =>
                    vehicle != null
                    && string.Equals(vehicle.Stock, stock, StringComparison.OrdinalIgnoreCase)))
                .Where(vehicle => vehicle != null)
                .ToArray();

            if (vehicles.Length != requestedStocks.Length) {
                return ComparisonError(HttpStatusCode.NotFound, "One or more selected vehicles are no longer available.");
            }

            var comparison = BuildVehicleComparison(vehicles);
            var aiCacheKey = "GTX:VehicleComparisonAi:DataOneV4Poem:"
                + Model.Inventory.Published.Ticks.ToString(CultureInfo.InvariantCulture)
                + ":"
                + string.Join("-", requestedStocks.OrderBy(stock => stock, StringComparer.OrdinalIgnoreCase));

            comparison.Analysis = ComparisonCache.Get(aiCacheKey) as VehicleComparisonAiAnalysis;
            if (comparison.Analysis == null) {
                var apiKey = ConfigurationManager.AppSettings["OpenAI:ApiKey"];
                var model = ConfigurationManager.AppSettings["OpenAI:ChatModel"];
                var responsesUrl = ConfigurationManager.AppSettings["OpenAI:ResponsesUrl"];

                if (string.IsNullOrWhiteSpace(apiKey)
                    || string.IsNullOrWhiteSpace(model)
                    || string.IsNullOrWhiteSpace(responsesUrl)) {
                    comparison.AiNotice = "The exact comparison is ready; the AI overview is not configured.";
                }
                else if (!AllowComparisonAiRequest()) {
                    comparison.AiNotice = "The exact comparison is ready; the AI overview is temporarily rate-limited.";
                }
                else {
                    try {
                        comparison.Analysis = await GetVehicleComparisonAnalysisAsync(
                            comparison,
                            apiKey.Trim(),
                            model.Trim(),
                            responsesUrl.Trim());

                        if (comparison.Analysis != null) {
                            ComparisonCache.Set(
                                aiCacheKey,
                                comparison.Analysis,
                                DateTimeOffset.UtcNow.AddMinutes(30));
                        }
                        else {
                            comparison.AiNotice = "The exact comparison is ready; the AI overview is temporarily unavailable.";
                        }
                    }
                    catch (Exception ex) {
                        Log("OpenAI vehicle comparison failed: " + ex.Message);
                        comparison.AiNotice = "The exact comparison is ready; the AI overview is temporarily unavailable.";
                    }
                }
            }

            return PartialView("_VehicleComparison", comparison);
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
                ViewBag.Stock = stock;
                ViewBag.RequestedUrl = Request?.Url?.AbsoluteUri;
                return View("VehicleNotFound");
            }

            try {
                vehicle.DetailsCounter = InventoryService.IncrementDetailsCounter(stock);
            }
            catch (Exception ex) {
                // Analytics must not prevent a customer from viewing the vehicle.
                System.Diagnostics.Trace.TraceError(
                    "Unable to increment the Details counter for stock {0}: {1}",
                    stock,
                    ex);
            }

            Model.CurrentVehicle.VehicleDetails = vehicle;
            Model.CurrentVehicle.VehicleDetails.Story = vehicle.Story;

            // If there is no DataOne get it
            if (Model.IsDataOne)
            {
                if (vehicle.DataOne == null && vehicle.HasDataOne)
                {
                    try
                    {
                        Model.CurrentVehicle.VehicleDataOneDetails = GetDecodedData(stock);
                    }
                    catch (Exception ex)
                    {
                        Log($"Saved DataOne details could not be loaded for stock {stock}: {ex.Message}");
                    }
                }
                else if (vehicle.DataOne == null)
                {
                    try
                    {
                        var details = VinDecoderService.DecodeVin(vehicle.VIN, dataOneApiKey, dataOneSecretApiKey);
                        var dataOne = Models.GTX.SetDecodedData(details);

                        if (dataOne != null)
                        {
                            InventoryService.SaveDataOneDetails(stock, details);
                            vehicle.HasDataOne = true;
                            vehicle.DataOne = dataOne;

                            // Query transmission 
                            var transmission = Model.CurrentVehicle.VehicleDetails.Transmission;

                            if (dataOne.QueryResponses?.Items != null)
                            {
                                foreach (var item in dataOne.QueryResponses.Items)
                                {
                                    if (item.UsMarketData.UsStyles.Styles.Count > 1)
                                    {
                                        item.UsMarketData.UsStyles.Styles = item.UsMarketData.UsStyles.Styles.Where(s => s.Transmissions?.Items?.Any(t => !string.IsNullOrWhiteSpace(t.Type) 
                                                && !string.IsNullOrWhiteSpace(transmission) && char.ToUpperInvariant(t.Type[0]) == char.ToUpperInvariant(transmission[0])) == true).ToList();
                                    }
                                }
                            }

                            Model.CurrentVehicle.VehicleDataOneDetails = dataOne;
                        }
                        else
                        {
                            Log($"DataOne decode returned no details for stock {stock}, VIN {vehicle.VIN}.");
                        }
                    }
                    catch (Exception ex)
                    {
                        Log($"DataOne decode failed for stock {stock}, VIN {vehicle.VIN}: {ex.Message}");
                    }
                }
                else {
                    Model.CurrentVehicle.VehicleDataOneDetails = vehicle.DataOne;
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
        public ActionResult All(string make, int? maximumYear, string color, string stocks) {
            var vehicles = Model?.Inventory?.All ?? Array.Empty<Models.GTX>();

            if (!string.IsNullOrWhiteSpace(stocks)) {
                var requestedStocks = new HashSet<string>(
                    stocks.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(stock => stock.Trim())
                        .Where(stock => stock.Length > 0 && stock.Length <= 20)
                        .Take(250),
                    StringComparer.OrdinalIgnoreCase);
                vehicles = vehicles
                    .Where(vehicle => vehicle != null && requestedStocks.Contains(vehicle.Stock ?? string.Empty))
                    .ToArray();
            }

            if (!string.IsNullOrWhiteSpace(make)) {
                var requestedMake = make.Trim();
                vehicles = vehicles
                    .Where(vehicle => string.Equals(vehicle.Make, requestedMake, StringComparison.OrdinalIgnoreCase))
                    .ToArray();
            }

            if (maximumYear.HasValue && maximumYear.Value >= 1886 && maximumYear.Value <= DateTime.UtcNow.Year + 2) {
                vehicles = vehicles.Where(vehicle => vehicle.Year <= maximumYear.Value).ToArray();
            }

            if (!string.IsNullOrWhiteSpace(color)) {
                var requestedColor = color.Trim();
                vehicles = vehicles
                    .Where(vehicle =>
                        (vehicle.Color ?? string.Empty).IndexOf(requestedColor, StringComparison.OrdinalIgnoreCase) >= 0
                        || (vehicle.Color2 ?? string.Empty).IndexOf(requestedColor, StringComparison.OrdinalIgnoreCase) >= 0)
                    .ToArray();
            }

            Model.Inventory.Vehicles = vehicles;
            Model.Inventory.Title = "All";
            ViewBag.Message = "Inventory";

            ViewBag.Title = I18n.F("Title_AllVehicles", Model.Inventory.Vehicles.Length);
            return View("Index", Model);
        }

        [HttpGet]
        public ActionResult Suvs(int? maximumPrice) {
            string body = CommonUnit.VehicleType.SUV.ToString();
            Model.Inventory.Suvs = GetOrEmpty(Model.Categories, body, Array.Empty<Models.GTX>());
            Model.Inventory.Vehicles = LimitMaximumPrice(Model?.Inventory?.Suvs, maximumPrice);

            Model.Inventory.Title = I18n.R("Nav_SUVs");
            ViewBag.Title = I18n.F("Title_Category", Model.Inventory.Vehicles.Length, Model.Inventory.Title);
            ViewBag.Message = "Inventory";
            return View("Index", Model);
        }

        [HttpGet]
        public ActionResult Sedans(int? maximumPrice) {
            string body = CommonUnit.VehicleType.SEDAN.ToString();
            Model.Inventory.Sedans = GetOrEmpty(Model.Categories, body, Array.Empty<Models.GTX>());
            Model.Inventory.Vehicles = LimitMaximumPrice(Model?.Inventory?.Sedans, maximumPrice);

            Model.Inventory.Title = I18n.R("Nav_Sedans");
            ViewBag.Title = I18n.F("Title_Category", Model.Inventory.Vehicles.Length, Model.Inventory.Title);
            ViewBag.Message = "Inventory";
            return View("Index", Model);
        }

        [HttpGet]
        public ActionResult Wagons(int? maximumPrice) {
            string body = CommonUnit.VehicleType.WAGON.ToString();
            Model.Inventory.Wagons = GetOrEmpty(Model.Categories, body, Array.Empty<Models.GTX>());
            Model.Inventory.Vehicles = LimitMaximumPrice(Model?.Inventory?.Wagons, maximumPrice);

            Model.Inventory.Title = I18n.R("Nav_Wagons");
            ViewBag.Title = I18n.F("Title_Category", Model.Inventory.Vehicles.Length, Model.Inventory.Title);
            ViewBag.Message = "Inventory";
            return View("Index", Model);
        }

        [HttpGet]
        public ActionResult Trucks(int? maximumPrice) {
            string body = CommonUnit.VehicleType.TRUCK.ToString();
            Model.Inventory.Trucks = GetOrEmpty(Model.Categories, body, Array.Empty<Models.GTX>());
            Model.Inventory.Vehicles = LimitMaximumPrice(Model?.Inventory?.Trucks, maximumPrice);

            Model.Inventory.Title = I18n.R("Nav_Trucks");
            ViewBag.Title = I18n.F("Title_Category", Model.Inventory.Vehicles.Length, Model.Inventory.Title);
            ViewBag.Message = "Inventory";
            return View("Index", Model);
        }

        [HttpGet]
        public ActionResult Vans(int? maximumPrice) {
            string body = CommonUnit.VehicleType.VAN.ToString();
            Model.Inventory.Vans = GetOrEmpty(Model.Categories, body, Array.Empty<Models.GTX>());
            Model.Inventory.Vehicles = LimitMaximumPrice(Model?.Inventory?.Vans, maximumPrice);

            Model.Inventory.Title = I18n.R("Nav_Vans");
            ViewBag.Title = I18n.F("Title_Category", Model.Inventory.Vehicles.Length, Model.Inventory.Title);
            ViewBag.Message = "Inventory";
            return View("Index", Model);
        }

        [HttpGet]
        public ActionResult Convertibles(int? maximumPrice) {
            string body = CommonUnit.VehicleType.CONVERTIBLE.ToString();
            Model.Inventory.Convertibles = GetOrEmpty(Model.Categories, body, Array.Empty<Models.GTX>());
            Model.Inventory.Vehicles = LimitMaximumPrice(Model?.Inventory?.Convertibles, maximumPrice);

            Model.Inventory.Title = I18n.R("Nav_Convertibles");
            ViewBag.Title = I18n.F("Title_Category", Model.Inventory.Vehicles.Length, Model.Inventory.Title);
            ViewBag.Message = "Inventory";
            return View("Index", Model);
        }

        [HttpGet]
        public ActionResult Hatchbacks(int? maximumPrice) {
            string body = CommonUnit.VehicleType.HATCHBACK.ToString();
            Model.Inventory.Hatchbacks = GetOrEmpty(Model.Categories, body, Array.Empty<Models.GTX>());
            Model.Inventory.Vehicles = LimitMaximumPrice(Model?.Inventory?.Hatchbacks, maximumPrice);

            Model.Inventory.Title = I18n.R("Nav_Hatchbacks");
            ViewBag.Title = I18n.F("Title_Category", Model.Inventory.Vehicles.Length, Model.Inventory.Title);
            ViewBag.Message = "Inventory";
            return View("Index", Model);
        }

        [HttpGet]
    public ActionResult Coupes(int? maximumPrice) {
            string body = CommonUnit.VehicleType.COUPE.ToString();
            Model.Inventory.Coupe = GetOrEmpty(Model.Categories, body, Array.Empty<Models.GTX>());
            Model.Inventory.Vehicles = LimitMaximumPrice(Model?.Inventory?.Coupe, maximumPrice);

            Model.Inventory.Title = I18n.R("Nav_Coupes");
            ViewBag.Title = I18n.F("Title_Category", Model.Inventory.Vehicles.Length, Model.Inventory.Title);
            ViewBag.Message = "Inventory";
            return View("Index", Model);
        }

        private static Models.GTX[] LimitMaximumPrice(IEnumerable<Models.GTX> vehicles, int? maximumPrice) {
            var results = vehicles ?? Enumerable.Empty<Models.GTX>();
            if (maximumPrice.HasValue && maximumPrice.Value > 0) {
                results = results.Where(vehicle => vehicle.InternetPrice > 0 && vehicle.InternetPrice <= maximumPrice.Value);
            }

            return results.ToArray();
        }

        [HttpPost]
        public JsonResult ApplyFilter(Filters model) {
            if (model == null) return Json(new { error = "Model was null" });

            if (model.Transmissions != null) model.Transmissions = model.Transmissions.Select(word => word.Substring(0, 1).ToUpper()).ToArray();

            Model.Inventory.Vehicles = ApplyFilters(model);
            Model.Inventory.Title = I18n.R("All_Search");
            ViewBag.Message = "Inventory";
            return Json(new { redirectUrl = Url.Content("~/Inventory/Index") });
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
            return Json(new { redirectUrl = Url.Content("~/Inventory/Index") });
        }

        [HttpPost]
        public JsonResult ApplyTerm(string term) {
            if (string.IsNullOrWhiteSpace(term)) {
                Model.Inventory.Vehicles = Model.Inventory.All;
                Model.Inventory.Title = "All";
                return Json(new { redirectUrl = Url.Content("~/Inventory/Index") });
            }

            term = term.Trim().ToUpper();

            Model.Inventory.Vehicles = ApplyTerms(term);
            Model.Inventory.Title = I18n.R("All_Search");
            return Json(new { redirectUrl = Url.Content("~/Inventory/Index") });
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

        private VehicleComparisonViewModel BuildVehicleComparison(Models.GTX[] vehicles) {
            var sources = vehicles.Select(BuildComparisonSource).ToArray();
            var comparison = new VehicleComparisonViewModel();

            foreach (var source in sources) {
                var vehicle = source.Vehicle;
                comparison.Vehicles.Add(new VehicleComparisonVehicle {
                    Stock = vehicle.Stock,
                    Title = JoinText(vehicle.Year.ToString(CultureInfo.InvariantCulture), vehicle.Make, vehicle.Model),
                    Subtitle = FirstText(source.Style?.BasicData?.Trim, vehicle.VehicleStyle),
                    DetailsUrl = Url.Action("Details", "Inventory", new { stock = vehicle.Stock }),
                    ImageUrl = InventoryImageUrl.Build(vehicle.Image, vehicle.Stock, InventoryImageVariant.Card),
                    HasDataOne = source.Style != null
                });
            }

            var overview = NewComparisonSection("Inventory overview", "bi-car-front-fill");
            AddComparisonRow(
                overview,
                "Transparent price",
                sources.Select(source => FormatMoney(source.Vehicle.InternetPrice > 0
                    ? source.Vehicle.InternetPrice + Constants.DOCUMENTARY_FEE
                    : source.Vehicle.InternetPrice)),
                sources.Select(source => source.Vehicle.InternetPrice > 0
                    ? (decimal?)(source.Vehicle.InternetPrice + Constants.DOCUMENTARY_FEE)
                    : null),
                preferLower: true);
            AddComparisonRow(
                overview,
                "Mileage",
                sources.Select(source => source.Vehicle.Mileage.ToString("N0", CultureInfo.GetCultureInfo("en-US")) + " miles"),
                sources.Select(source => (decimal?)source.Vehicle.Mileage),
                preferLower: true);
            AddComparisonRow(overview, "Exterior color", sources.Select(source => source.Vehicle.Color));
            AddComparisonRow(overview, "Interior color", sources.Select(source => source.Vehicle.Color2));
            comparison.Sections.Add(overview);

            var identity = NewComparisonSection("Vehicle details", "bi-card-checklist");
            AddComparisonRow(identity, "Body style", sources.Select(source => FirstText(source.Style?.BasicData?.BodyType, source.Style?.BasicData?.OemBodyStyle, source.Vehicle.VehicleStyle, source.Vehicle.Body)));
            AddComparisonRow(identity, "Trim", sources.Select(source => FirstText(source.Style?.BasicData?.Trim, source.Vehicle.VehicleStyle)));
            AddComparisonRow(identity, "Package", sources.Select(source => source.Style?.BasicData?.PackageSummary));
            AddComparisonRow(identity, "Doors", sources.Select(source => source.Style?.BasicData?.Doors), sources.Select(source => ParseNumber(source.Style?.BasicData?.Doors)));
            comparison.Sections.Add(identity);

            var powertrain = NewComparisonSection("Powertrain", "bi-gear-wide-connected");
            AddComparisonRow(powertrain, "Engine", sources.Select(EngineDescription));
            AddComparisonRow(powertrain, "Fuel type", sources.Select(source => FirstText(source.Engine?.FuelType, source.Vehicle.FuelType)));
            AddComparisonRow(powertrain, "Drive type", sources.Select(source => FirstText(source.Style?.BasicData?.DriveType, source.Vehicle.DriveTrain)));
            AddComparisonRow(powertrain, "Transmission", sources.Select(TransmissionDescription));
            AddComparisonRow(
                powertrain,
                "Horsepower",
                sources.Select(source => WithUnit(FirstText(source.Engine?.TotalMaxHp, source.Engine?.IceMaxHp, source.Engine?.ElectricMaxHp), "hp")),
                sources.Select(source => ParseNumber(FirstText(source.Engine?.TotalMaxHp, source.Engine?.IceMaxHp, source.Engine?.ElectricMaxHp))),
                preferLower: false);
            AddComparisonRow(
                powertrain,
                "Torque",
                sources.Select(source => WithUnit(FirstText(source.Engine?.TotalMaxTorque, source.Engine?.IceMaxTorque, source.Engine?.ElectricMaxTorque), "lb-ft")),
                sources.Select(source => ParseNumber(FirstText(source.Engine?.TotalMaxTorque, source.Engine?.IceMaxTorque, source.Engine?.ElectricMaxTorque))),
                preferLower: false);
            AddComparisonRow(
                powertrain,
                "Cylinders",
                sources.Select(source => FirstText(source.Engine?.IceCylinders, source.Vehicle.Cylinders > 0 ? source.Vehicle.Cylinders.ToString(CultureInfo.InvariantCulture) : null)),
                sources.Select(source => ParseNumber(FirstText(source.Engine?.IceCylinders, source.Vehicle.Cylinders > 0 ? source.Vehicle.Cylinders.ToString(CultureInfo.InvariantCulture) : null))));
            comparison.Sections.Add(powertrain);

            var efficiency = NewComparisonSection("Fuel efficiency", "bi-fuel-pump-fill");
            AddComparisonRow(efficiency, "City MPG", sources.Select(source => WithUnit(source.Epa?.City, "MPG")), sources.Select(source => ParseNumber(source.Epa?.City)), preferLower: false);
            AddComparisonRow(efficiency, "Highway MPG", sources.Select(source => WithUnit(source.Epa?.Highway, "MPG")), sources.Select(source => ParseNumber(source.Epa?.Highway)), preferLower: false);
            AddComparisonRow(efficiency, "Combined MPG", sources.Select(source => WithUnit(source.Epa?.Combined, "MPG")), sources.Select(source => ParseNumber(source.Epa?.Combined)), preferLower: false);
            AddComparisonRow(efficiency, "Fuel tank", sources.Select(source => FindSpecification(source.Style, @"fuel.*(tank|capacity)")));
            if (efficiency.Rows.Count > 0) comparison.Sections.Add(efficiency);

            var capacity = NewComparisonSection("Capacity and utility", "bi-rulers");
            AddComparisonRow(
                capacity,
                "Seating capacity",
                sources.Select(source => FindSpecification(source.Style, @"(seating|passenger).*capacity|maximum.*(seating|passenger)")),
                sources.Select(source => ParseNumber(FindSpecification(source.Style, @"(seating|passenger).*capacity|maximum.*(seating|passenger)"))),
                preferLower: false);
            AddComparisonRow(
                capacity,
                "Maximum payload",
                sources.Select(source => FirstText(source.Engine?.MaxPayload, FindSpecification(source.Style, @"maximum.*payload|payload.*capacity"))),
                sources.Select(source => ParseNumber(FirstText(source.Engine?.MaxPayload, FindSpecification(source.Style, @"maximum.*payload|payload.*capacity")))),
                preferLower: false);
            AddComparisonRow(
                capacity,
                "Maximum towing",
                sources.Select(source => FindSpecification(source.Style, @"maximum.*tow|tow.*capacity")),
                sources.Select(source => ParseNumber(FindSpecification(source.Style, @"maximum.*tow|tow.*capacity"))),
                preferLower: false);
            AddComparisonRow(
                capacity,
                "Cargo volume",
                sources.Select(source => FindSpecification(source.Style, @"cargo.*(volume|capacity)")),
                sources.Select(source => ParseNumber(FindSpecification(source.Style, @"cargo.*(volume|capacity)"))),
                preferLower: false);
            AddComparisonRow(capacity, "Curb weight", sources.Select(source => FirstText(FindSpecification(source.Style, @"curb.*weight"), source.Vehicle.Weight > 0 ? source.Vehicle.Weight.ToString("N0", CultureInfo.GetCultureInfo("en-US")) + " lb" : null)));
            if (capacity.Rows.Count > 0) comparison.Sections.Add(capacity);

            var dimensions = NewComparisonSection("Dimensions", "bi-arrows-angle-expand");
            AddComparisonRow(dimensions, "Wheelbase", sources.Select(source => FindSpecification(source.Style, @"wheelbase")));
            AddComparisonRow(dimensions, "Overall length", sources.Select(source => FindSpecification(source.Style, @"overall.*length|^length$")));
            AddComparisonRow(dimensions, "Overall width", sources.Select(source => FindSpecification(source.Style, @"overall.*width|^width$")));
            AddComparisonRow(dimensions, "Overall height", sources.Select(source => FindSpecification(source.Style, @"overall.*height|^height$")));
            AddComparisonRow(dimensions, "Ground clearance", sources.Select(source => FindSpecification(source.Style, @"ground.*clearance")));
            if (dimensions.Rows.Count > 0) comparison.Sections.Add(dimensions);

            var safety = NewComparisonSection("Safety", "bi-shield-check");
            AddComparisonRow(
                safety,
                "NHTSA overall rating",
                sources.Select(source => WithUnit(source.Style?.NhtsaCrashTestRatings?.OverallStars, "stars")),
                sources.Select(source => ParseNumber(source.Style?.NhtsaCrashTestRatings?.OverallStars)),
                preferLower: false);
            if (safety.Rows.Count > 0) comparison.Sections.Add(safety);

            return comparison;
        }

        private static ComparisonSource BuildComparisonSource(Models.GTX vehicle) {
            var style = SelectComparisonStyle(vehicle);
            var engine = SelectComparisonEngine(vehicle, style);
            var transmission = SelectComparisonTransmission(vehicle, style);
            var epa = SelectComparisonEpa(style, engine, transmission);
            return new ComparisonSource(vehicle, style, engine, transmission, epa);
        }

        private static Style SelectComparisonStyle(Models.GTX vehicle) {
            var styles = vehicle?.DataOne?.QueryResponses?.Items?
                .Where(response => response?.UsMarketData?.UsStyles?.Styles != null)
                .SelectMany(response => response.UsMarketData.UsStyles.Styles)
                .Where(style => style != null)
                .ToArray() ?? Array.Empty<Style>();

            if (styles.Length == 0) return null;
            if (styles.Length == 1) return styles[0];

            var rankedStyles = styles
                .Select(style => new { Style = style, Score = ComparisonStyleScore(vehicle, style) })
                .Where(candidate => candidate.Score >= 25)
                .OrderByDescending(candidate => candidate.Score)
                .ToArray();
            if (rankedStyles.Length == 0) return null;

            var bestStyles = rankedStyles
                .Where(candidate => candidate.Score == rankedStyles[0].Score)
                .Select(candidate => candidate.Style)
                .ToArray();
            if (bestStyles.Length == 1) return bestStyles[0];

            // Multiple style IDs are safe only when the compared DataOne content is identical.
            var fingerprints = bestStyles.Select(ComparisonStyleFingerprint).Distinct(StringComparer.Ordinal).ToArray();
            return fingerprints.Length == 1 ? bestStyles[0] : null;
        }

        private static int ComparisonStyleScore(Models.GTX vehicle, Style style) {
            var score = 0;
            var styleTerm = NormalizeComparisonText(vehicle?.VehicleStyle);
            var trim = NormalizeComparisonText(style?.BasicData?.Trim);
            var styleText = NormalizeComparisonText(JoinText(
                style?.Name,
                style?.BasicData?.Trim,
                style?.BasicData?.OemBodyStyle,
                style?.BasicData?.PackageSummary));

            if (styleTerm.Length > 0) {
                if (styleTerm == trim) score += 100;
                else if (styleText.Contains(styleTerm)) score += 60;
                else score += ComparisonTokenOverlap(styleTerm, styleText) * 8;
            }

            var vehicleDrive = NormalizeDriveType(vehicle?.DriveTrain);
            var dataOneDrive = NormalizeDriveType(style?.BasicData?.DriveType);
            if (vehicleDrive.Length > 0 && dataOneDrive.Length > 0) score += vehicleDrive == dataOneDrive ? 30 : -25;

            if (vehicle != null && vehicle.Cylinders > 0 && style?.Engines?.Items?.Any() == true) {
                score += style.Engines.Items.Any(engine => engine != null
                    && string.Equals(engine.IceCylinders, vehicle.Cylinders.ToString(CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase))
                    ? 15
                    : -15;
            }

            if (!string.IsNullOrWhiteSpace(vehicle?.FuelType) && style?.Engines?.Items?.Any() == true) {
                var fuel = NormalizeComparisonText(vehicle.FuelType);
                if (style.Engines.Items.Any(engine => NormalizeComparisonText(engine?.FuelType).Contains(fuel))) score += 10;
            }

            if (!string.IsNullOrWhiteSpace(vehicle?.Transmission) && style?.Transmissions?.Items?.Any() == true) {
                var code = char.ToUpperInvariant(vehicle.Transmission.Trim()[0]);
                if (style.Transmissions.Items.Any(transmission => transmission != null
                    && !string.IsNullOrWhiteSpace(transmission.Type)
                    && char.ToUpperInvariant(transmission.Type[0]) == code)) score += 8;
            }

            var vehicleBody = NormalizeComparisonText(JoinText(vehicle?.Body, vehicle?.VehicleType));
            var dataOneBody = NormalizeComparisonText(JoinText(style?.BasicData?.BodyType, style?.BasicData?.BodySubtype, style?.BasicData?.OemBodyStyle));
            score += Math.Min(ComparisonTokenOverlap(vehicleBody, dataOneBody) * 4, 12);
            return score;
        }

        private static int ComparisonTokenOverlap(string first, string second) {
            if (first.Length == 0 || second.Length == 0) return 0;
            var secondTokens = new HashSet<string>(second.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries), StringComparer.Ordinal);
            return first.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Count(token => token.Length > 1 && secondTokens.Contains(token));
        }

        private static string NormalizeComparisonText(string value) {
            return Regex.Replace((value ?? string.Empty).ToUpperInvariant(), @"[^A-Z0-9]+", " ").Trim();
        }

        private static string NormalizeDriveType(string value) {
            var normalized = NormalizeComparisonText(value);
            if (Regex.IsMatch(normalized, @"\b(AWD|ALL WHEEL)\b")) return "AWD";
            if (Regex.IsMatch(normalized, @"\b(4WD|FOUR WHEEL|4X4)\b")) return "4WD";
            if (Regex.IsMatch(normalized, @"\b(FWD|FRONT WHEEL)\b")) return "FWD";
            if (Regex.IsMatch(normalized, @"\b(RWD|REAR WHEEL)\b")) return "RWD";
            return normalized;
        }

        private static string ComparisonStyleFingerprint(Style style) {
            var values = new List<string> {
                NormalizeComparisonText(style?.BasicData?.Trim),
                NormalizeComparisonText(style?.BasicData?.BodyType),
                NormalizeDriveType(style?.BasicData?.DriveType),
                NormalizeComparisonText(style?.BasicData?.Doors)
            };
            values.AddRange((style?.Engines?.Items ?? new List<Engine>())
                .Where(engine => engine != null)
                .Select(engine => JoinText(engine.IceCylinders, engine.IceDisplacement, engine.FuelType, engine.TotalMaxHp, engine.IceMaxHp, engine.TotalMaxTorque, engine.IceMaxTorque))
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase));
            values.AddRange((style?.Transmissions?.Items ?? new List<GTX.Models.Transmission>())
                .Where(transmission => transmission != null)
                .Select(transmission => JoinText(transmission.Type, transmission.Gears, transmission.DetailType))
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase));
            values.AddRange((style?.StandardSpecifications?.Categories ?? new List<SpecificationCategory>())
                .Where(category => category != null)
                .SelectMany(category => (category.Values ?? new List<SpecificationValue>())
                    .Where(value => value != null)
                    .Select(value => JoinText(category.Name, value.Name, value.Value)))
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase));
            return string.Join("|", values);
        }

        private static Engine SelectComparisonEngine(Models.GTX vehicle, Style style) {
            var engines = style?.Engines?.Items?.Where(item => item != null).ToArray() ?? Array.Empty<Engine>();
            if (engines.Length == 1) return engines[0];

            var standard = engines.Where(item => ContainsWord(item.Availability, "standard")).ToArray();
            if (standard.Length == 1) return standard[0];

            if (vehicle != null && vehicle.Cylinders > 0) {
                var matches = engines.Where(item => string.Equals(
                    item.IceCylinders,
                    vehicle.Cylinders.ToString(CultureInfo.InvariantCulture),
                    StringComparison.OrdinalIgnoreCase)).ToArray();
                if (matches.Length == 1) return matches[0];
            }

            return null;
        }

        private static GTX.Models.Transmission SelectComparisonTransmission(Models.GTX vehicle, Style style) {
            var transmissions = style?.Transmissions?.Items?.Where(item => item != null).ToArray()
                ?? Array.Empty<GTX.Models.Transmission>();
            if (transmissions.Length == 1) return transmissions[0];

            if (vehicle != null && !string.IsNullOrWhiteSpace(vehicle.Transmission)) {
                var code = char.ToUpperInvariant(vehicle.Transmission.Trim()[0]);
                var matches = transmissions.Where(item => !string.IsNullOrWhiteSpace(item.Type)
                    && char.ToUpperInvariant(item.Type[0]) == code).ToArray();
                if (matches.Length == 1) return matches[0];
            }

            var standard = transmissions.Where(item => ContainsWord(item.Availability, "standard")).ToArray();
            return standard.Length == 1 ? standard[0] : null;
        }

        private static EpaMpgRecord SelectComparisonEpa(Style style, Engine engine, GTX.Models.Transmission transmission) {
            var records = style?.EpaFuelEfficiency?.Records?.Where(item => item != null).ToArray()
                ?? Array.Empty<EpaMpgRecord>();
            if (records.Length == 1) return records[0];

            var matches = records.Where(record =>
                (engine == null || string.IsNullOrWhiteSpace(record.EngineId) || string.Equals(record.EngineId, engine.EngineId, StringComparison.OrdinalIgnoreCase))
                && (transmission == null || string.IsNullOrWhiteSpace(record.TransmissionId) || string.Equals(record.TransmissionId, transmission.TransmissionId, StringComparison.OrdinalIgnoreCase)))
                .ToArray();
            return matches.Length == 1 ? matches[0] : null;
        }

        private static VehicleComparisonSection NewComparisonSection(string name, string icon) {
            return new VehicleComparisonSection { Name = name, Icon = icon };
        }

        private static void AddComparisonRow(
            VehicleComparisonSection section,
            string label,
            IEnumerable<string> values,
            IEnumerable<decimal?> numericValues = null,
            bool preferLower = false) {
            var normalized = values.Select(value => string.IsNullOrWhiteSpace(value) ? "Not provided" : value.Trim()).ToList();
            if (normalized.All(value => string.Equals(value, "Not provided", StringComparison.Ordinal))) return;

            var row = new VehicleComparisonRow { Label = label, Values = normalized };
            row.Highlights = Enumerable.Repeat(false, normalized.Count).ToList();

            var numbers = numericValues?.ToList();
            if (numbers != null && numbers.Count == normalized.Count && numbers.Count(value => value.HasValue) >= 2) {
                var best = preferLower
                    ? numbers.Where(value => value.HasValue).Min(value => value.Value)
                    : numbers.Where(value => value.HasValue).Max(value => value.Value);
                var bestIndexes = numbers
                    .Select((value, index) => new { value, index })
                    .Where(item => item.value.HasValue && item.value.Value == best)
                    .Select(item => item.index)
                    .ToArray();
                if (bestIndexes.Length == 1) row.Highlights[bestIndexes[0]] = true;
            }

            section.Rows.Add(row);
        }

        private static string FindSpecification(Style style, string pattern) {
            var categories = style?.StandardSpecifications?.Categories ?? new List<SpecificationCategory>();
            foreach (var category in categories.Where(item => item != null)) {
                foreach (var value in (category.Values ?? new List<SpecificationValue>()).Where(item => item != null)) {
                    var name = JoinText(category.Name, value.Name);
                    if (Regex.IsMatch(name, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)) {
                        return value.Value;
                    }
                }
            }
            return null;
        }

        private static string EngineDescription(ComparisonSource source) {
            if (source.Engine == null) return source.Vehicle.Engine;
            return FirstText(
                source.Engine.MarketingName,
                source.Engine.Name,
                JoinText(
                    WithUnit(source.Engine.IceDisplacement, "L"),
                    source.Engine.IceAspiration,
                    source.Engine.IceBlockType,
                    WithUnit(source.Engine.IceCylinders, "cylinders")),
                source.Vehicle.Engine);
        }

        private static string TransmissionDescription(ComparisonSource source) {
            if (source.Transmission == null) return FirstText(source.Vehicle.TransmissionWord, source.Vehicle.Transmission);
            return FirstText(
                source.Transmission.MarketingName,
                source.Transmission.Name,
                JoinText(WithUnit(source.Transmission.Gears, "speed"), source.Transmission.DetailType, source.Transmission.Type));
        }

        private static string WithUnit(string value, string unit) {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim() + " " + unit;
        }

        private static string FirstText(params string[] values) {
            return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
        }

        private static string JoinText(params string[] values) {
            return string.Join(" ", values.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()));
        }

        private static bool ContainsWord(string value, string word) {
            return !string.IsNullOrWhiteSpace(value)
                && value.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static decimal? ParseNumber(string value) {
            if (string.IsNullOrWhiteSpace(value)) return null;
            var match = Regex.Match(value, @"-?\d+(?:\.\d+)?", RegexOptions.CultureInvariant);
            decimal number;
            return match.Success && decimal.TryParse(match.Value, NumberStyles.Number, CultureInfo.InvariantCulture, out number)
                ? number
                : (decimal?)null;
        }

        private static string FormatMoney(int value) {
            return value > 0
                ? value.ToString("C0", CultureInfo.GetCultureInfo("en-US"))
                : "Call for price";
        }

        private JsonResult ComparisonError(HttpStatusCode statusCode, string message) {
            Response.StatusCode = (int)statusCode;
            return Json(new { message });
        }

        private async Task<VehicleComparisonAiAnalysis> GetVehicleComparisonAnalysisAsync(
            VehicleComparisonViewModel comparison,
            string apiKey,
            string model,
            string responsesUrl) {
            var vehicles = new JArray();
            for (var index = 0; index < comparison.Vehicles.Count; index++) {
                var specifications = new JObject();
                foreach (var section in comparison.Sections) {
                    foreach (var row in section.Rows) {
                        var value = row.Values[index];
                        if (!string.Equals(value, "Not provided", StringComparison.OrdinalIgnoreCase)) {
                            specifications[section.Name + " - " + row.Label] = value;
                        }
                    }
                }

                vehicles.Add(new JObject {
                    ["stock"] = comparison.Vehicles[index].Stock,
                    ["vehicle"] = comparison.Vehicles[index].Title + " " + comparison.Vehicles[index].Subtitle,
                    ["specifications"] = specifications
                });
            }

            var schema = JObject.Parse(@"{
                'type':'object',
                'properties':{
                    'summary':{'type':'string'},
                    'recommendations':{
                        'type':'array','minItems':2,'maxItems':4,
                        'items':{
                            'type':'object',
                            'properties':{
                                'stock':{'type':'string'},
                                'bestFor':{'type':'string'},
                                'reason':{'type':'string'}
                            },
                            'required':['stock','bestFor','reason'],
                            'additionalProperties':false
                        }
                    },
                    'caveats':{'type':'array','maxItems':4,'items':{'type':'string'}}
                },
                'required':['summary','recommendations','caveats'],
                'additionalProperties':false
            }");

            var payload = new JObject {
                ["model"] = model,
                ["instructions"] = "Compare only the supplied verified vehicle values. Never invent, estimate, repair, omit, or alter facts, numbers, or units to force a rhyme. Write the summary as exactly four short rhyming lines separated by newlines. Give one recommendation for every selected stock number. Keep bestFor as a short practical shopper label. Write each reason as exactly four short rhyming lines separated by newlines, explaining meaningful factual tradeoffs. Keep the rhyme natural and compact, avoid declaring one universal winner, use no heading or blank line, and do not output HTML.",
                ["input"] = new JArray(new JObject {
                    ["role"] = "user",
                    ["content"] = "Analyze these vehicles for practical shoppers: " + vehicles.ToString(Formatting.None)
                }),
                ["text"] = new JObject {
                    ["format"] = new JObject {
                        ["type"] = "json_schema",
                        ["name"] = "gtx_vehicle_comparison",
                        ["strict"] = true,
                        ["schema"] = schema
                    }
                },
                ["max_output_tokens"] = 650,
                ["temperature"] = 0.2,
                ["store"] = false
            };

            using (var request = new HttpRequestMessage(HttpMethod.Post, responsesUrl)) {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
                request.Content = new StringContent(payload.ToString(Formatting.None), Encoding.UTF8, "application/json");

                using (var response = await ComparisonOpenAiClient.SendAsync(request)) {
                    OpenAiRateLimitHealth.Capture(response.Headers, model);
                    var body = await response.Content.ReadAsStringAsync();
                    if (!response.IsSuccessStatusCode) {
                        throw new HttpRequestException("OpenAI comparison request failed with status " + (int)response.StatusCode + ".");
                    }

                    var outputText = ExtractComparisonOutputText(JObject.Parse(body));
                    if (string.IsNullOrWhiteSpace(outputText)) return null;
                    var analysis = JObject.Parse(outputText).ToObject<VehicleComparisonAiAnalysis>();
                    return IsValidComparisonAnalysis(analysis, comparison.Vehicles)
                        ? analysis
                        : null;
                }
            }
        }

        private static bool IsValidComparisonAnalysis(
            VehicleComparisonAiAnalysis analysis,
            IList<VehicleComparisonVehicle> vehicles) {
            if (analysis == null
                || string.IsNullOrWhiteSpace(analysis.Summary)
                || analysis.Recommendations == null
                || analysis.Recommendations.Count != vehicles.Count) {
                return false;
            }

            var expectedStocks = new HashSet<string>(
                vehicles.Select(vehicle => vehicle.Stock),
                StringComparer.OrdinalIgnoreCase);
            var recommendationStocks = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var recommendation in analysis.Recommendations) {
                if (recommendation == null
                    || string.IsNullOrWhiteSpace(recommendation.Stock)
                    || string.IsNullOrWhiteSpace(recommendation.BestFor)
                    || string.IsNullOrWhiteSpace(recommendation.Reason)
                    || !expectedStocks.Contains(recommendation.Stock)
                    || !recommendationStocks.Add(recommendation.Stock)) {
                    return false;
                }
            }

            return recommendationStocks.SetEquals(expectedStocks);
        }

        private static string ExtractComparisonOutputText(JObject response) {
            var text = response["output"]
                ?.Children<JObject>()
                .Where(item => string.Equals((string)item["type"], "message", StringComparison.OrdinalIgnoreCase))
                .SelectMany(item => item["content"]?.Children<JObject>() ?? Enumerable.Empty<JObject>())
                .Where(item => string.Equals((string)item["type"], "output_text", StringComparison.OrdinalIgnoreCase))
                .Select(item => (string)item["text"])
                .Where(item => !string.IsNullOrWhiteSpace(item));
            return string.Join(Environment.NewLine, text ?? Enumerable.Empty<string>());
        }

        private bool AllowComparisonAiRequest() {
            var identity = Request?.UserHostAddress ?? "unknown";
            var key = "GTX:VehicleComparisonRate:" + identity;
            lock (ComparisonRateLock) {
                var counter = ComparisonCache.Get(key) as ComparisonRequestCounter;
                if (counter == null) {
                    counter = new ComparisonRequestCounter();
                    ComparisonCache.Set(key, counter, DateTimeOffset.UtcNow.AddMinutes(10));
                }
                if (counter.Count >= 10) return false;
                counter.Count++;
                return true;
            }
        }

        private static HttpClient CreateComparisonOpenAiClient() {
            int timeoutSeconds;
            if (!int.TryParse(ConfigurationManager.AppSettings["OpenAI:ChatTimeoutSeconds"], out timeoutSeconds)
                || timeoutSeconds < 5
                || timeoutSeconds > 120) {
                timeoutSeconds = 30;
            }
            return new HttpClient { Timeout = TimeSpan.FromSeconds(timeoutSeconds) };
        }

        private sealed class ComparisonSource {
            public ComparisonSource(Models.GTX vehicle, Style style, Engine engine, GTX.Models.Transmission transmission, EpaMpgRecord epa) {
                Vehicle = vehicle;
                Style = style;
                Engine = engine;
                Transmission = transmission;
                Epa = epa;
            }

            public Models.GTX Vehicle { get; }
            public Style Style { get; }
            public Engine Engine { get; }
            public GTX.Models.Transmission Transmission { get; }
            public EpaMpgRecord Epa { get; }
        }

        private sealed class ComparisonRequestCounter {
            public int Count { get; set; }
        }
    }
}
