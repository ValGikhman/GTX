using GTX.Models;
using Newtonsoft.Json;
using Services;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web.Mvc;
using System.Web.Script.Serialization;

namespace GTX.Controllers {

    public class InventoryController : BaseController {
        private readonly string openAiApiKey = ConfigurationManager.AppSettings["OpenAI:ApiKey"];

        public InventoryController(ISessionData sessionData, IInventoryService inventoryService, ILogService LogService)
            : base(sessionData, inventoryService, LogService) {
        }

        public ActionResult Index(BaseModel model) {
            Model.Inventory.Title = "All";
            ViewBag.Title = $"{Model.Inventory.Title} inventory ({Model.Inventory.Vehicles.Length}) vehicles";
            Log($"{Model.Inventory.Title} inventory");
            return View(Model);
        }

        public ActionResult DetailsCard(string stock) {
            Model.CurrentVehicle.VehicleDetails = Model.Inventory.All.FirstOrDefault(m => m.Stock == stock);
            return View("DetailsCard", Model.CurrentVehicle.VehicleDetails);
        }

            public async Task<ActionResult> Details(string stock) {
            Model.Inventory.Title = "Details";
            stock = stock?.Trim().ToUpper();
            if (string.IsNullOrEmpty(stock)) {
                Model.Inventory.Title = "All";
                Model.Inventory.Vehicles = SessionData?.Inventory?.All;
                ViewBag.Title = $"{Model.Inventory.Title} inventory ({Model.Inventory.Vehicles.Length}) vehicles";
                return View("Index", Model);
            }

            Model.CurrentVehicle.VehicleDetails = Model.Inventory.All.FirstOrDefault(m => m.Stock == stock);
            Model.CurrentVehicle.VehicleImages = GetImages(stock);
            SessionData.CurrentVehicle = Model.CurrentVehicle;
            Model.CurrentVehicle.VehicleSuggesion = Model.Inventory.All.Where(m => m.Stock != stock && Math.Abs(m.RetailPrice - Model.CurrentVehicle.VehicleDetails.RetailPrice) < 3000).Take(10).ToArray();
            ViewBag.Title = $"{Model.CurrentVehicle.VehicleDetails.Year} - {Model.CurrentVehicle.VehicleDetails.Make} - {Model.CurrentVehicle.VehicleDetails.Model} {Model.CurrentVehicle.VehicleDetails.VehicleStyle} ";

            Model.CurrentVehicle.VehicleDetails.Story = InventoryService.GetStory(stock);
            return View("Details", Model);

        }

        public ActionResult DetailsModal(string stock) {
            stock = stock.Trim().ToUpper();
            if (stock != null) {
                Model.CurrentVehicle.VehicleDetails = Model.Inventory.Vehicles.FirstOrDefault(m => m.Stock == stock);
                Model.CurrentVehicle.VehicleImages = GetImages(stock);
                SessionData.CurrentVehicle = Model.CurrentVehicle;
            }

            return PartialView("_DetailModal", Model.CurrentVehicle);
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
                catch(Exception ex) {
                    return Content("Unable to fetch Carfax report.");
                }
            }
        }

        public ActionResult All() {
            Model.Inventory.Vehicles = SessionData?.Inventory?.All;
            Model.Inventory.Title = "All";
            ViewBag.Title = $"{Model.Inventory.Title} inventory ({Model.Inventory.Vehicles.Length}) vehicles";

            return View("Index", Model);
        }

        public ActionResult Cars() {
            Model.Inventory.Vehicles = SessionData?.Inventory?.Cars;
            Model.Inventory.Title = "Cars";
            ViewBag.Title = $"{Model.Inventory.Title} inventory ({Model.Inventory.Vehicles.Length}) vehicles";

            return View("Index", Model);
        }

        public ActionResult Suvs() {
            Model.Inventory.Vehicles = SessionData?.Inventory?.Suvs;
            Model.Inventory.Title = "Suvs";
            ViewBag.Title = $"{Model.Inventory.Title} inventory ({Model.Inventory.Vehicles.Length}) vehicles";

            return View("Index", Model);
        }

        public ActionResult Sedans() {
            Model.Inventory.Vehicles = SessionData?.Inventory?.Sedans;
            Model.Inventory.Title = "Sedans";
            ViewBag.Title = $"{Model.Inventory.Title} inventory ({Model.Inventory.Vehicles.Length}) vehicles";

            return View("Index", Model);
        }

        public ActionResult Wagons() {
            Model.Inventory.Vehicles = SessionData?.Inventory?.Wagons;
            Model.Inventory.Title = "Wagons";
            ViewBag.Title = $"{Model.Inventory.Title} inventory ({Model.Inventory.Vehicles.Length}) vehicles";

            return View("Index", Model);
        }
        
        public ActionResult Trucks() {
            Model.Inventory.Vehicles = SessionData?.Inventory?.Trucks;
            Model.Inventory.Title = "Trucks";
            ViewBag.Title = $"{Model.Inventory.Title} inventory ({Model.Inventory.Vehicles.Length}) vehicles";

            return View("Index", Model);
        }

        public ActionResult Vans() {
            Model.Inventory.Vehicles = SessionData?.Inventory?.Vans;
            Model.Inventory.Title = "Vans";
            ViewBag.Title = $"{Model.Inventory.Title} inventory ({Model.Inventory.Vehicles.Length}) vehicles";

            return View("Index", Model);
        }

        public ActionResult Cargo() {
            Model.Inventory.Vehicles = SessionData?.Inventory?.Cargo;
            Model.Inventory.Title = "Cargo";
            ViewBag.Title = $"{Model.Inventory.Title} inventory ({Model.Inventory.Vehicles.Length}) vehicles";

            return View("Index", Model);
        }

        public ActionResult Convertibles() {
            Model.Inventory.Vehicles = SessionData?.Inventory?.Convertibles;
            Model.Inventory.Title = "Convertibles";
            ViewBag.Title = $"{Model.Inventory.Title} inventory ({Model.Inventory.Vehicles.Length}) vehicles";

            return View("Index", Model);
        }

        public ActionResult Hatchbacks() {
            Model.Inventory.Vehicles = SessionData?.Inventory?.Hatchbacks;
            Model.Inventory.Title = "Hatchbacks";
            ViewBag.Title = $"{Model.Inventory.Title} inventory ({Model.Inventory.Vehicles.Length}) vehicles";

            return View("Index", Model);
        }

        public ActionResult Coupes() {
            Model.Inventory.Vehicles = SessionData?.Inventory?.Coupe;
            Model.Inventory.Title = "Coupes";
            ViewBag.Title = $"{Model.Inventory.Title} inventory ({Model.Inventory.Vehicles.Length}) vehicles";

            return View("Index", Model);
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
        public JsonResult ApplyTerm(string term) {
            term = term.Trim().ToUpper();
            Log($"Applying term: {term}");
            Model.CurrentFilter = null;
            Model.Inventory.Vehicles = ApplyTerms(term);
            Model.Inventory.Title = "Search";
            return Json(new { redirectUrl = Url.Action("Index") });
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
        public JsonResult GetVehicleTypes(string makes) {
            try {
                if (!string.IsNullOrEmpty(makes)) {
                    string[] request = new JavaScriptSerializer().Deserialize<string[]>(makes);
                    return Json(SessionData?.Inventory.All?.Where(m => request.Contains(m.Make)).Select(m => m.VehicleType).Distinct().OrderBy(m => m).ToArray(), JsonRequestBehavior.AllowGet);
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
                    milesMax = SessionData?.Inventory?.All?.Where(m => request.Contains(m.Make)).Max(m => m.Mileage);
                    milesMin = SessionData?.Inventory?.All?.Where(m => request.Contains(m.Make)).Min(m => m.Mileage);
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

            return query.OrderBy(m => m.Make).ToArray();
        }

        public string[] GetImages(string stock) {
            string path = $"~/GTXImages/Inventory/{stock}";
            string[] extensions = new[] { ".jpg", ".jpeg", ".png", ".gif" };
            string imagesPath = Server.MapPath($"{path}");
            List<string> imageUrls = new List<string>();

            if (Directory.Exists(imagesPath)) {
                string[] imageFiles = Directory.GetFiles(imagesPath).Where(file => extensions.Contains(Path.GetExtension(file).ToLower())).ToArray();
                foreach (string file in imageFiles) {
                    string fileName = Path.GetFileName(file);
                    imageUrls.Add(Url.Content($"{path}/{fileName}"));
                }
            }

            Model.CurrentVehicle.VehicleImages = imageUrls.ToArray();
            return Model.CurrentVehicle.VehicleImages;
        }

        [HttpPost]
        public async Task<JsonResult> DecodeVin(string vin) {
            return Json(new { Error = "Not ready yet" }, JsonRequestBehavior.AllowGet);

            using (HttpClient client = new HttpClient()) {
                string url = $"https://www.vinaudit.com/vin-decoder?vin={vin}";
                HttpResponseMessage response = await client.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                    return Json(new { Error = "Error fetching VIN data." }, JsonRequestBehavior.AllowGet);

                var data = await response.Content.ReadAsHttpResponseMessageAsync();
                return Json(new { data }, JsonRequestBehavior.AllowGet);
            }
        }

        private async Task<string> GetChatGptResponse(string prompt) {
            var apiUrl = "https://api.openai.com/v1/chat/completions";

            using (var httpClient = new HttpClient()) {
                httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {openAiApiKey}");

                var requestBody = new {
/*                    model = "gpt-3.5-turbo",   // or "gpt-4o" if your account supports it
*/                    model = "gpt-4o",  
                    messages = new[]
                    {
                        new { role = "user", content = prompt }
                    },
                    max_tokens = 500,
                    temperature = 0.7
                };

                var content = new StringContent(JsonConvert.SerializeObject(requestBody), Encoding.UTF8, "application/json");

                var response = await httpClient.PostAsync(apiUrl, content);

                if (response.IsSuccessStatusCode) {
                    var jsonResponse = await response.Content.ReadAsStringAsync();
                    dynamic result = JsonConvert.DeserializeObject(jsonResponse);

                    return result.choices[0].message.content.ToString();
                }
                else {
                    return $"Error: {response.StatusCode}";
                }
            }
        }
    }
}