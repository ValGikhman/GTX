using GTX.Models;
using Services;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Web.Mvc;

namespace GTX.Controllers {

    public abstract class BaseController : Controller {

        #region Properties
        public readonly String devComputer = "VALS-PC";

        public readonly string imageFolder = "/GTXImages/Inventory/";
        public readonly string openAiApiKey = ConfigurationManager.AppSettings["OpenAI:ApiKey"];

        private static readonly object _sync = new object();
        private static readonly Random _rand = new Random();
        private static int Version() { lock (_sync) return _rand.Next(1, 1); }

        public ILogService LogService { get; set; }

        public IInventoryService InventoryService { get; set; }

        public ISessionData SessionData { get; private set; }

        public BaseModel Model { get; set; }

        #endregion Properties

        #region Construtors

        public BaseController(ISessionData _sessionData, IInventoryService _invntoryService, ILogService _logService) {
            SessionData = _sessionData;
            LogService = _logService;
            InventoryService = _invntoryService;
        }

        #endregion Construtors

        #region Overrides

        protected override void Initialize(System.Web.Routing.RequestContext requestContext) {
            base.Initialize(requestContext);
        }

        protected async override void OnActionExecuting(ActionExecutingContext filterContext) {
            base.OnActionExecuting(filterContext);

            try {
                Model = new BaseModel();

                Model.IsDevelopment = (Environment.GetEnvironmentVariable("COMPUTERNAME") == devComputer);
                SessionData.SetSession(Constants.SESSION_ENVIRONMENT, Model.IsDevelopment ? "Development" : "Production");
                ViewBag.Environment = SessionData.Environment;

                if (SessionData == null || SessionData?.IsMajordome == null) {
                    Model.IsMajordome = false;
                    SessionData.SetSession(Constants.SESSION_MAJORDOME, Model.IsMajordome);
                }

                Model.IsMajordome = (bool)SessionData.IsMajordome;
                ViewBag.IsMajordome = Model.IsMajordome;

                if (SessionData == null || SessionData?.Inventory == null) {
                    Model.Inventory = await SetModel(Model.Inventory);
                    SessionData.SetSession(Constants.SESSION_INVENTORY, Model.Inventory);
                }

                Model.Inventory = SessionData.Inventory;

                if (SessionData == null || SessionData?.Employers == null) {
                    Employer[] employers = await Utility.XMLHelpers.XmlRepository.GetEmployers();
                    SessionData.SetSession(Constants.SESSION_EMPLOYERS, employers);
                }

                Model.Employers = SessionData.Employers;

                if (SessionData == null || SessionData?.Filters == null) {
                    Filters filters = BuildFilters(Model.Inventory);
                    SessionData.SetSession(Constants.SESSION_FILTERS, filters);
                }

                if (SessionData == null ||  SessionData?.OpenHours == null) {
                    OpenHours[] openHours = Utility.XMLHelpers.XmlRepository.GetOpenHours();
                    SessionData.SetSession(Constants.SESSION_OPEN_HOURS, openHours);
                    Model.OpenHours = openHours;                
                }

                Model.OpenHours = SessionData.OpenHours;
            }
            catch (Exception ex) {
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

        private async Task<Inventory> SetModel(Inventory model) {
            if (SessionData?.Inventory == null) {
                var emptyArray = Array.Empty<Models.GTX>();

                model.Current = await Utility.XMLHelpers.XmlRepository.GetInventory();
                model.Current = ApplyImagesAndStories(model.Current);

                model.All = model.Current
                    .Where(m => m.SetToUpload == "Y" && !string.IsNullOrWhiteSpace(m.PurchaseDate))
                    .OrderByDescending(m => DateTime.TryParse(m.PurchaseDate, out var date) ? date : DateTime.MinValue)
                    .ToArray();
                
                model.Vehicles = model.All;

                string SUV = CommonUnit.VehicleType.SUV.ToString();
                string TRUCK = CommonUnit.VehicleType.TRUCK.ToString();
                string VAN = CommonUnit.VehicleType.VAN.ToString();
                string HATCHBACK = CommonUnit.VehicleType.HATCHBACK.ToString();
                string CONVERTIBLE = CommonUnit.VehicleType.CONVERTIBLE.ToString();
                string SEDAN = CommonUnit.VehicleType.SEDAN.ToString();
                string COUPE = CommonUnit.VehicleType.COUPE.ToString();
                string WAGON = CommonUnit.VehicleType.WAGON.ToString();

                var byType = model.All
                    .GroupBy(v => v.VehicleType == null ? "" : v.VehicleType.Trim(), StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.ToArray(), StringComparer.OrdinalIgnoreCase);

                model.Suvs = GetOrEmpty(byType, SUV, emptyArray);
                model.Trucks = GetOrEmpty(byType, TRUCK, emptyArray);
                model.Vans = GetOrEmpty(byType, VAN, emptyArray);
                model.Hatchbacks = GetOrEmpty(byType, HATCHBACK, emptyArray);
                model.Convertibles = GetOrEmpty(byType, CONVERTIBLE, emptyArray);
                model.Sedans = GetOrEmpty(byType, SEDAN, emptyArray);
                model.Coupe = GetOrEmpty(byType, COUPE, emptyArray);
                model.Wagons = GetOrEmpty(byType, WAGON, emptyArray);

                return model;
            }

            return SessionData.Inventory;
        }

        [HttpGet]
        public JsonResult GetNow() {
            try {
                string returnValue;
                string currentDay = DateTime.Now.DayOfWeek.ToString();
                int currentHour = DateTime.Now.Hour;

                var today = Model.OpenHours.FirstOrDefault(m => m.Day == currentDay);
                bool isOpened = (currentHour >= today.From && currentHour <= today.To);
                string openClose = isOpened ? "Now opened" : "Closed";
/*                if (today.From == 0 && today.To == 0) {
                    returnValue = $"{today.Day}: {today.Description}";
                }
                else {
                    returnValue = $"{today.Day}: {today.Description} - {openClose}";
                }*/

                returnValue = $"{openClose}";

                return Json(new { Now = returnValue }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex) {
                Log(ex);
            }
            finally {
            }
            return null;
        }

        public Image[] GetImages(string stock) {
            return InventoryService.GetImages(stock);
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

        public Models.GTX[] ApplyImagesAndStories(Models.GTX[] vehicles) {
            foreach (var vehicle in vehicles) {
                vehicle.Story = InventoryService.GetStory(vehicle.Stock);
                vehicle.Images = InventoryService.GetImages(vehicle.Stock);

                vehicle.Image = $"{imageFolder}no-image-{Version()}.jpg";
                if (vehicle.Images != null && vehicle.Images.Length > 0) {
                    vehicle.Image = $"{imageFolder}{vehicle.Stock}/{vehicle.Images[0].Name}"; ;
                }
            }

            return vehicles;
        }

        [HttpPost]
        public ActionResult Reset() {
            Model.Inventory.Vehicles = Model.Inventory.All;
            return Json(new { redirectUrl = Url.Action("All") });
        }

        public Models.GTX[] ApplyTerms(string term) {
            Models.GTX[] query = Model.Inventory.All;

            if (query.Any() && term != null) {
                query = query.Where(m => m.Stock.ToUpper().Contains(term)
                    || m.VIN.ToUpper().Contains(term)
                    || m.Year.ToString() == term
                    || m.Make.ToUpper().Contains(term)
                    || m.Model.ToUpper().Contains(term)
                    || m.VehicleStyle.ToUpper().Contains(term))
                .Distinct().ToArray();
            }

            return query.OrderBy(m => m.Make).ToArray();
        }

        public void TerminateSession() {
            Session.Clear();
            Session.RemoveAll();
            Session.Abandon();
        }
        #endregion Public Methods


        #region private methods
        private static Filters BuildFilters(Inventory inv) {
            var all = inv.All;

            return new Filters {
                Makes = all.Select(x => x.Make).Distinct().OrderBy(x => x).ToArray(),
                Models = all.Select(x => x.Model).Distinct().OrderBy(x => x).ToArray(),
                Engines = all.Select(x => x.Engine).Distinct().OrderBy(x => x).ToArray(),
                FuelTypes = all.Select(x => x.FuelType).Distinct().OrderBy(x => x).ToArray(),
                DriveTrains = all.Select(x => x.DriveTrain).Distinct().OrderBy(x => x).ToArray(),
                BodyTypes = all.Select(x => x.Body).Distinct().OrderBy(x => x).ToArray(),
                VehicleTypes = all.Select(x => x.VehicleType).Distinct().OrderBy(x => x).ToArray(),
                MaxPrice = all.Max(x => x.RetailPrice),
                MinPrice = all.Min(x => x.RetailPrice)
            };
        }

        // Helper (regular method, no expression-bodied, no 'out var')
        private static Models.GTX[] GetOrEmpty(Dictionary<string, Models.GTX[]> dict, string key, Models.GTX[] empty) {
            Models.GTX[] arr;
            if (dict.TryGetValue(key, out arr)) return arr;
            return empty;
        }


        #endregion
    }
}