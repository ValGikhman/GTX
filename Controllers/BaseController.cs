using GTX.Models;
using Services;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Web.Mvc;
using System.Xml.Linq;
using System.Xml.Serialization;

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

                if (SessionData == null || SessionData?.IsMajordome == null) {
                    Model.IsMajordome = false;
                    SessionData.SetSession(Constants.SESSION_MAJORDOME, Model.IsMajordome);
                }

                Model.IsMajordome = (bool)SessionData.IsMajordome;
                ViewBag.IsMajordome = Model.IsMajordome;

                if (SessionData == null || SessionData?.EZ360Inventory == null) {
                    Model.EZ360Inventory = EZ360Service.GetInventory(ez360ProjectId);
                    SessionData.SetSession(Constants.SESSION_EZ360_INVENTORY, Model.EZ360Inventory);
                }
                Model.EZ360Inventory = SessionData.EZ360Inventory;

                if (SessionData == null || SessionData?.Inventory == null)
                {
                    Model.Inventory = SetModel(Model.Inventory);
                    SessionData.SetSession(Constants.SESSION_INVENTORY, Model.Inventory);
                }
                Model.Inventory = SessionData.Inventory;

                if (SessionData == null || SessionData?.Employers == null) {
                    Employer[] employers = Utility.XMLHelpers.XmlRepository.GetEmployers();
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

        private Inventory SetModel(Inventory model) {
            if (SessionData?.Inventory == null) {
                var emptyArray = Array.Empty<Models.GTX>();

                if (Model.IsEZ360)
                {
                    model.Current = VehicleMapper.ToGTXInventory(Model.EZ360Inventory);
                }
                else {
                    model.Current = Utility.XMLHelpers.XmlRepository.GetInventory();
                }

                model.Current = ApplyExtended(model.Current);

                model.All = model.Current
                    .Where(m => m.SetToUpload == "Y")
                    .OrderBy(m => m.Make).ThenBy(m => m.Model)
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
                string returnValue = Model.OpenHours.FirstOrDefault(m => m.Day == DateTime.Now.DayOfWeek.ToString()) is { } today &&  DateTime.Now.Hour >= today.From && DateTime.Now.Hour <= today.To
                    ? "Now open"
                    : "Closed";
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
            if (!Model.CurrentVehicle.DisplayEZ360Player && Model.IsEZ360) {
                var currentVehicle = Model.EZ360Inventory.FirstOrDefault(m => m.StockNo == stock);
                return currentVehicle.ThirdPartyPics.Select(m => new Image() { Id = Guid.Empty, Stock = currentVehicle.StockNo, DateCreated = DateTime.Now, Order = 0, Source = m }).ToArray();
            }
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

        public Models.GTX[] ApplyExtended(Models.GTX[] vehicles) {
            foreach (var vehicle in vehicles) {
                // vehicle.Story = InventoryService.GetStory(vehicle.Stock);
                // vehicle.DataOne = GetDecodedData(vehicle.Stock);
                vehicle.TransmissionWord = WordIt(vehicle.Transmission);

                vehicle.Image = $"{imageFolder}no-image-{Version()}.jpg";

                if (!Model.IsEZ360) {
                    vehicle.Images = InventoryService.GetImages(vehicle.Stock);
                    if (vehicle.Images != null && vehicle.Images.Length > 0)
                    {
                        vehicle.Image = $"{imageFolder}{vehicle.Images[0].Source}"; ;
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

        public Models.GTX[] ApplyTerms(string term) {
            Models.GTX[] query = Model.Inventory.All;

            if (query.Any() && term != null) {
                query = query.Where(m => m.Stock.ToUpper().Contains(term)
                    || m.VIN.ToUpper().Contains(term)
                    || m.Year.ToString() == term
                    || m.Make.ToUpper().Contains(term)
                    || m.TransmissionWord.ToUpper().Contains(term)
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

        public static string WordIt(string? transmission) {
            try {
                string res = string.Empty;
                if (transmission != null) {
                    switch (transmission) {
                        case "A":
                            return "Automatic";

                        case "M":
                            return "Manual";

                        case "T":
                            return "Transverse";

                        case "C":
                            return "Continuously variable";

                        default:
                            return transmission;
                    }
                }

                return transmission;
            }
            catch {
                return "N/A";
            }
        }

        public DecodedData GetDecodedData(string stock) {
            string dataOne = InventoryService.GetDataOneDetails(stock);

            var (errCode, errMsg) = ParseDecoderError(dataOne);

            if (errCode != null && errCode != "RI") {
                Console.WriteLine(errMsg);
                return null;
            }

            try {
                var serializer = new XmlSerializer(typeof(DecodedData));
                using (TextReader reader = new StringReader(dataOne))
                {
                    return (DecodedData)serializer.Deserialize(reader);
                }
            }
            catch(Exception ex) {
                return null;
            }
        }

        #endregion Public Methods


        #region private methods
        private static Filters BuildFilters(Inventory inv) {
            var all = inv.All;

            return new Filters {
                Makes = all.Select(x => x.Make).Distinct().OrderBy(x => x).ToArray(),
                Models = all.Select(x => x.Model).Distinct().OrderBy(x => x).ToArray(),
                Cylinders = all.Select(x => x.Cylinders.ToString()).Distinct().OrderBy(x => x).ToArray(),
                Transmissions = all.Select(x => WordIt(x.Transmission)).Distinct().OrderBy(x => x).ToArray(),
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

        private static (string? code, string? message) ParseDecoderError(string xml) {
            try {
                var doc = System.Xml.Linq.XDocument.Parse(xml);
                var err = doc.Descendants("decoder_errors").Descendants("error").FirstOrDefault();
                if (err == null) return (null, null);

                var code = (string?)err.Element("code");
                var msg = (string?)err.Element("message");

                if (code == "RI") return (null, null);

                return (code, msg);
            }
            catch {
                // If it isn't valid XML, treat as a body/format error
                return ("PARSE", "Invalid XML from decoder");
            }
        }
        #endregion
    }
}