using GTX.Models;
using Services;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Web.Mvc;

namespace GTX.Controllers
{

    public abstract class BaseController : Controller {

        #region Properties
        public readonly string devComputer = "VALS-PC";

        public readonly string imageFolder = "/GTXImages/Inventory/";
        public readonly string openAiApiKey = ConfigurationManager.AppSettings["OpenAI:ApiKey"];
        public readonly string dataOneApiKey = ConfigurationManager.AppSettings["DataOne:AccessKey"];
        public readonly string dataOneSecretApiKey = ConfigurationManager.AppSettings["DataOne:SecretAccessKey"];
        public readonly string ez360ProjectId = ConfigurationManager.AppSettings["EZ360:ProjectId"];

        private static readonly object _sync = new object();
        private static readonly Random _rand = new Random();
        private static int Version() { lock (_sync) return _rand.Next(1, 1); }

        public ILogService LogService { get; set; }

        public IInventoryService InventoryService { get; set; }

        public IVinDecoderService VinDecoderService { get; set; }

        public ISessionData SessionData { get; private set; }

        public IEZ360Service EZ360Service { get; private set; }

        public BaseModel Model { get; set; }

        #endregion Properties

        #region Construtors

        public BaseController(ISessionData _sessionData, IInventoryService _invntoryService, IVinDecoderService _vinDecoderService, IEZ360Service _ez360Service, ILogService _logService) {
            SessionData = _sessionData;
            LogService = _logService;
            InventoryService = _invntoryService;
            VinDecoderService = _vinDecoderService;
            EZ360Service = _ez360Service;
        }
        public BaseController(ISessionData _sessionData)
        {
            SessionData = _sessionData;
        }

        #endregion Construtors

        #region Overrides

        protected override void Initialize(System.Web.Routing.RequestContext requestContext) {
            base.Initialize(requestContext);
        }

        protected override void OnActionExecuting(ActionExecutingContext filterContext) {
            base.OnActionExecuting(filterContext);

            try {
                Model = new BaseModel();

                Model.IsDevelopment = (Environment.GetEnvironmentVariable("COMPUTERNAME") == devComputer);
                Model.IsEZ360 = ConfigurationManager.AppSettings["isEZ360"] == "true";
                Model.IsDataOne = ConfigurationManager.AppSettings["isDataOne"] == "true";

                SessionData.SetSession(Constants.SESSION_ENVIRONMENT, Model.IsDevelopment ? "Development" : "Production");
                ViewBag.Environment = SessionData.Environment;

                if (SessionData == null || SessionData?.IsMajordome == null)
                {
                    Model.IsMajordome = false;
                    SessionData.SetSession(Constants.SESSION_MAJORDOME, Model.IsMajordome);
                }

                Model.IsMajordome = (bool)SessionData.IsMajordome;
                ViewBag.IsMajordome = Model.IsMajordome;

                Model.Inventory = AppCache.GetOrCreate(Constants.INVENTORY_CACHE, () => SetModel(), minutes: 60);
                Model.Employers = AppCache.GetOrCreate(Constants.EMPLOYERS_CACHE, () => Utility.XMLHelpers.XmlRepository.GetEmployers(), minutes: 60);
                Model.OpenHours = AppCache.GetOrCreate(Constants.OPENHOURS_CACHE, () => Utility.XMLHelpers.XmlRepository.GetOpenHours(), minutes: 60);
                Model.Filters = AppCache.GetOrCreate(Constants.FILTERS_CACHE, () => BuildFilters(Model.Inventory), minutes: 60);
                Model.Categories = AppCache.GetOrCreate(Constants.CATEGORIES_CACHE, () => GetCategories(), minutes: 60);
                Model.Passwords = AppCache.GetOrCreate(Constants.PASSWORDS_CACHE, () => GetPasswords(), minutes: 60);

                var roleStr = AppCache.GetOrCreate(Constants.ROLE_CACHE, () => CommonUnit.Roles.User.ToString(),  minutes: 60);
                Model.CurrentRole = Enum.TryParse(roleStr, ignoreCase: true, out CommonUnit.Roles role)  ? role : CommonUnit.Roles.User;

                var published = Model.Inventory?.Published ?? DateTime.Now;
                ViewBag.Published = Model.IsDevelopment ? published : published.AddHours(-5);

            }
            catch (Exception ex) {
                Log($"OnActionExecuting error: {ex.Message}");
            }
        }

        protected override void OnAuthorization(AuthorizationContext filterContext) {
            try {
                base.OnAuthorization(filterContext);
            }
            catch (Exception ex) {
                throw ex;
            }
        }

        #endregion Overrides

        #region Public

        public void Log(Exception ex) {
            LogService.Log(SessionData.LogHeader, ex);
        }

        public void Log(string action) {
            LogService.Log(SessionData.LogHeader, action);
        }

        public void Log(Log header, CommonUnit.LogType logType) {
            LogService.Log(header, logType);
        }

        #endregion public

        #region Public Methods
        [HttpPost]
        protected bool ValidateLogin(string password)
        {
            password = (password ?? "").Trim();

            var expected = Model.Passwords.Where(m => m.Password.Equals(password)).FirstOrDefault();
            if (expected == null) return false;

            if (Enum.TryParse<CommonUnit.Roles>(expected.Role, ignoreCase: true, out var role))
            {
                Model.CurrentRole = role;
            }
            else
            {
                Model.CurrentRole = CommonUnit.Roles.User;
            }

            AppCache.Remove(Constants.ROLE_CACHE);
            AppCache.GetOrCreate(Constants.ROLE_CACHE, () => role.ToString(), minutes: 60);

            return true;
        }

        public static string RenderViewToString(ControllerContext context, string viewName) {
            return RenderViewToString(context, viewName, null);
        }

        public static string RenderViewToString(ControllerContext context, string viewName, object model) {
            if (string.IsNullOrEmpty(viewName))
                viewName = context.RouteData.GetRequiredString("action");

            ViewDataDictionary viewData = new ViewDataDictionary(model);

            using (StringWriter sw = new StringWriter()) {
                ViewEngineResult viewResult = ViewEngines.Engines.FindPartialView(context, viewName);
                ViewContext viewContext = new ViewContext(context, viewResult.View, viewData, new TempDataDictionary(), sw);
                viewResult.View.Render(viewContext, sw);

                return sw.GetStringBuilder().ToString();
            }
        }

        public Inventory SetModel() {
            var dto = InventoryService.GetInventory();
            var vehicles = Models.GTX.ToGTX(dto.vehicles);
            Model.Inventory.Published = dto.InventoryDate;
            Model.Inventory.All = DecideImages(vehicles);

            Model.Inventory.Vehicles = Model.Inventory.All;
            return Model.Inventory;
        }

        public Inventory RefreshModel()
        {
            var dto = InventoryService.GetInventory();
            var vehicles = Models.GTX.ToGTX(dto.vehicles);
            Model.Inventory.Published = dto.InventoryDate;
            Model.Inventory.All = DecideImages(vehicles);

            Model.Inventory.Vehicles = Model.Inventory.All;
            return Model.Inventory;
        }

        [HttpGet]
        public JsonResult GetNow(int offset) // offset in minutes from JS
        {
            try
            {
                // Server time in UTC
                var utcNow = DateTime.UtcNow;

                // Convert to user local time
                var userNow = utcNow.AddMinutes(-offset);
                var todayName = userNow.DayOfWeek.ToString();
                var today = Model.OpenHours?.FirstOrDefault(m => m.Day == todayName);
                bool isOpen = today != null && userNow.Hour >= today.From && userNow.Hour <= today.To;
                var returnValue = isOpen ? "Now open" : "Closed";

                return Json(new { Now = returnValue }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                Log(ex);
                return Json(new { Now = "Closed" }, JsonRequestBehavior.AllowGet);
            }
        }

        public static string SerializeModel(object model) {
            try {
                if (model == null) {
                    return "null";
                }

                var options = new JsonSerializerOptions {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    IgnoreNullValues = true
                };
                return JsonSerializer.Serialize(model, options);
            }

            catch (Exception ex) {
                return $"Serialization Error: {ex.Message}";
            }
        }

        public Models.GTX[] DecideImages(Models.GTX[] vehicles)
        {
            if (vehicles == null || vehicles.Length == 0)
                return vehicles ?? Array.Empty<Models.GTX>();

            // Compute once instead of per vehicle
            var defaultImage = $"{imageFolder}no-image-{Version()}.jpg";
            var isEz360 = Model.IsEZ360;

            foreach (var vehicle in vehicles)
            {
                vehicle.Image = defaultImage;
                var stockImages = InventoryService.GetImages(vehicle.Stock);

                if (!isEz360)
                {
                    if (stockImages != null && stockImages.Length > 0)
                    {
                        vehicle.Images = stockImages;
                        vehicle.Image = $"{imageFolder}{stockImages[0].Source}";
                    }
                    else
                    {
                        vehicle.Images = Array.Empty<Image>();
                    }
                }
                else
                {
                    var ez360 = vehicle.EZ360;
                    var ezImages = PickPrimaryImages(ez360);

                    // Normalize to avoid repeated null checks
                    var hasEz = ezImages is { Length: > 0 };
                    var hasStock = stockImages is { Length: > 0 };

                    if (!hasEz && !hasStock)
                    {
                        vehicle.Images = Array.Empty<Image>();
                    }
                    else if (hasEz && !hasStock)
                    {
                        vehicle.Images = ezImages;
                    }
                    else if (!hasEz && hasStock)
                    {
                        vehicle.Images = stockImages;
                    }
                    else
                    {
                        // both have values: ez + stock
                        var merged = new Image[ezImages.Length + stockImages.Length];
                        Array.Copy(ezImages, 0, merged, 0, ezImages.Length);
                        Array.Copy(stockImages, 0, merged, ezImages.Length, stockImages.Length);
                        vehicle.Images = merged;
                    }

                    var chosen = PickPrimaryImage(ez360, 200);
                    if (!string.IsNullOrWhiteSpace(chosen))
                    {
                        vehicle.Image = chosen;
                    }
                }
            }

            return vehicles;
        }

        [HttpPost]
        public ActionResult Reset() {
            Model.Inventory.Vehicles = Model.Inventory.All;
            return Json(new { redirectUrl = Url.Action("All") });
        }

        public Models.GTX[] ApplyFilters(Filters filter) {
            var request = new QueryHelper<Models.GTX>(Model.Inventory.All);
            request
                .InList(m => m.Make, filter.Makes)
                .InList(m => m.Model, filter.Models)
                .InList(m => m.Cylinders.ToString(), filter.Cylinders)
                .InList(m => m.Transmission, filter.Transmissions)
                .InList(m => m.DriveTrain, filter.DriveTrains)
                .InList(m => m.Body, filter.BodyTypes)
                .InList(m => m.FuelType, filter.FuelTypes)
                .InList(m => m.VehicleType, filter.VehicleTypes)
                .Between(m => m.Mileage, filter.MinMilege, filter.MaxMilege)
                .Between(m => m.InternetPrice, filter.MinPrice, filter.MaxPrice);
            return request.Query.OrderBy(m => m.Make).ThenBy(m => m.Model).ToArray();
        }

        public Models.GTX[] ApplyTerms(string term)
        {
            var all = Model.Inventory.All ?? Array.Empty<Models.GTX>();

            // If no term, just return everything ordered
            if (string.IsNullOrWhiteSpace(term) || all.Length == 0)
                return all.OrderBy(m => m.Make).ToArray();

            term = term.Trim();
            var isYear = int.TryParse(term, out var year);

            var termUpper = term.ToUpperInvariant();

            var filtered = all.Where(m =>
                (!string.IsNullOrEmpty(m.Stock) &&
                    m.Stock.ToUpperInvariant().Contains(termUpper)) ||

                (!string.IsNullOrEmpty(m.VIN) &&
                    m.VIN.ToUpperInvariant().Contains(termUpper)) ||

                (isYear && m.Year == year) ||

                (!string.IsNullOrEmpty(m.Make) &&
                    m.Make.ToUpperInvariant().Contains(termUpper)) ||

                (!string.IsNullOrEmpty(m.TransmissionWord) &&
                    m.TransmissionWord.ToUpperInvariant().Contains(termUpper)) ||

                (!string.IsNullOrEmpty(m.Model) &&
                    m.Model.ToUpperInvariant().Contains(termUpper)) ||

                (!string.IsNullOrEmpty(m.VehicleStyle) &&
                    m.VehicleStyle.ToUpperInvariant().Contains(termUpper))
            );

            return filtered.OrderBy(m => m.Make).ToArray();
        }

        public void TerminateSession() {
            AppCache.ClearAll();
            Session.Clear();
            Session.RemoveAll();
            Session.Abandon();
        }

        public DecodedData GetDecodedData(string stock) {
            string dataOne = InventoryService.GetDataOneDetails(stock);

            return Models.GTX.SetDecodedData(dataOne);
        }
        #endregion Public Methods

        #region private methods
        private Dictionary<string, Models.GTX[]> GetCategories() {

            return Model?.Inventory?.All.GroupBy(v => v.VehicleType == null ? "" : v.VehicleType.Trim(), StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.ToArray(), StringComparer.OrdinalIgnoreCase);

        }

        private static Filters BuildFilters(Inventory inventory)
        {
            var all = inventory.All;
            return new Filters
            {
                Makes = BuildFilter(all, m => m.Make),
                Models = BuildFilter(all, m => m.Model),
                Cylinders = BuildFilter(all, m => m.Cylinders.ToString(), ignoreNullOrWhiteSpace: false),
                Transmissions = BuildFilter(all, m => Models.GTX.WordIt(m.Transmission)),
                FuelTypes = BuildFilter(all, m => m.FuelType),
                DriveTrains = BuildFilter(all, m => m.DriveTrain),
                BodyTypes = BuildFilter(all, m => m.Body),
                VehicleTypes = BuildFilter(all, m => m.VehicleType),
                MaxPrice = all.Max(m => m.InternetPrice),
                MinPrice = all.Min(m => m.InternetPrice)
            };
        }

        public static Models.GTX[] GetOrEmpty(Dictionary<string, Models.GTX[]> dict, string key, Models.GTX[] empty) {
            Models.GTX[] arr;
            if (dict.TryGetValue(key, out arr)) return arr;
            return empty;
        }

        private static string? PickPrimaryImage(EZ360.Vehicle? ez, int height = 200)
        {
            if (ez == null) return null;

            var display = ez.DisplayPics?.FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(display)) {
                var finalUrl = display.Contains("?")
                    ? display + "&h=200"
                    : display + "?h=200";
                return finalUrl;
            }

            var third = ez.ThirdPartyPics?.FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(third)) return third;

            return null;
        }

        private Image[] PickPrimaryImages(EZ360.Vehicle? ez)
        {
            if (ez == null) return null;

            if (ez.DetailPics != null && ez.DetailPics.Any())
            {
                return ez.DetailPics.Select(m => new Image() { Id = Guid.Empty, Stock = ez.StockNo, DateCreated = DateTime.Now, Order = 0, Source = m }).ToArray();
            }

            if (ez.ThirdPartyPics != null && ez.ThirdPartyPics.Any()) {
                return ez.ThirdPartyPics.Select(m => new Image() { Id = Guid.Empty, Stock = ez.StockNo, DateCreated = DateTime.Now, Order = 0, Source = m }).ToArray();
            }

            return null;
        }

        private static string[] BuildFilter<T>(IEnumerable<T> source, Func<T, string> selector, bool ignoreNullOrWhiteSpace = true)
        {
            var query = source.Select(selector);

            if (ignoreNullOrWhiteSpace) query = query.Where(s => !string.IsNullOrWhiteSpace(s));

            return query.Distinct().OrderBy(s => s).ToArray();
        }

        #endregion

        #region Passwords
        private List<SitePassword> GetPasswords()
        {
            return ConfigurationManager.AppSettings.AllKeys.Where(k => k.StartsWith("sitePassword:", StringComparison.OrdinalIgnoreCase))
                .Select(k => new SitePassword {
                    Role = k.Substring("sitePassword:".Length),
                    Password = ConfigurationManager.AppSettings[k]
                }).ToList();
        }
        #endregion
    }
}