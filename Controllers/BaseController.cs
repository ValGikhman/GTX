using GTX.Session;
using Services;
using System;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Web.Mvc;
using System.Web.Routing;
using System.Threading.Tasks;
using GTX.Models;
using System.Linq;
using System.Text.Json;
using System.Collections.Generic;

namespace GTX.Controllers {

    public abstract class BaseController : Controller {

        #region Properties

        public ILogService LogService { get; set; }

        public ISessionData SessionData { get; private set; }

        public BaseModel Model { get; set; }

        #endregion Properties

        #region Construtors

        public BaseController(ISessionData sessionData, ILogService _logService) {
            SessionData = sessionData;
            LogService = _logService;

            Model = new BaseModel();
        }

        #endregion Construtors

        #region Overrides

        protected override void Initialize(System.Web.Routing.RequestContext requestContext) {
            base.Initialize(requestContext);
        }

        protected async override void OnActionExecuting(ActionExecutingContext filterContext) {
            base.OnActionExecuting(filterContext);

            try {
                BaseModel model = new BaseModel();
                if (SessionData?.Inventory == null) {
                    model.Inventory = await SetModel(model.Inventory);
                    SessionData.SetSession(Constants.SESSION_INVENTORY, model.Inventory);
                }
                model.Inventory = SessionData.Inventory;

                if (SessionData?.Employers == null) {
                    Employer[] employers = await Utility.XMLHelpers.XmlRepository.GetEmployers();
                    SessionData.SetSession(Constants.SESSION_EMPLOYERS, employers);
                }

                model.Employers = SessionData.Employers;

                if (SessionData?.Filters == null) {
                    Filters filters = new Filters();
                    filters.Makes = model.Inventory.All.Select(m => m.Make).Distinct().OrderBy(m => m).ToArray();
                    filters.Models = model.Inventory.All.Select(m => m.Model).Distinct().OrderBy(m => m).ToArray();
                    filters.Engines = model.Inventory.All.Select(m => m.Engine).Distinct().OrderBy(m => m).ToArray();
                    filters.FuelTypes = model.Inventory.All.Select(m => m.FuelType).Distinct().OrderBy(m => m).ToArray();
                    filters.MaxPrice = model.Inventory.All.Max(m => m.RetailPrice);
                    filters.MinPrice = model.Inventory.All.Min(m => m.RetailPrice);
                    filters.DriveTrains = model.Inventory.All.Select(m => m.DriveTrain).Distinct().OrderBy(m => m).ToArray();
                    filters.BodyTypes = model.Inventory.All.Select(m => m.Body).Distinct().OrderBy(m => m).ToArray();
                    SessionData.SetSession(Constants.SESSION_FILTERS, filters);
                }

                if (SessionData?.OpenHours == null) {
                    OpenHours[] openHours = Utility.XMLHelpers.XmlRepository.GetOpenHours();
                    SessionData.SetSession(Constants.SESSION_OOPEN_HOURS, openHours);
                }

                model.Employers = SessionData.Employers;

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

        #region public

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
                model.All = await Utility.XMLHelpers.XmlRepository.GetInventory();
                model.All = model.All.Where(m => m.RetailPrice > 0).OrderByDescending(m => m.PurchaseDate).ThenBy(m => m.Make).ToArray(); ;

                var carTypes = new HashSet<string> {
                    CommonUnit.VehicleType.SEDAN.ToString(),
                    CommonUnit.VehicleType.COUPE.ToString(),
                    CommonUnit.VehicleType.CONVERTIBLE.ToString(),
                    CommonUnit.VehicleType.HATCHBACK.ToString()
                };

                model.Cars = model.All.Where(m => carTypes.Contains(m.VehicleType.ToUpper())).ToArray();
                model.Suvs = model.All.Where(m => m.VehicleType.ToUpper().Equals(CommonUnit.VehicleType.SUV.ToString())).ToArray();
                model.Trucks = model.All.Where(m => m.VehicleType.ToUpper().Equals(CommonUnit.VehicleType.TRUCK.ToString())).ToArray();
                model.Vans = model.All.Where(m => m.VehicleType.ToUpper().Equals(CommonUnit.VehicleType.VAN.ToString())).ToArray();

                return model;
            }

            return SessionData.Inventory;
        }

        [HttpGet]
        public JsonResult GetNow() {
            try {

                string currentDay = DateTime.Now.DayOfWeek.ToString();
                int currentHour = DateTime.Now.Hour;

                var today = Model.OpenHours.FirstOrDefault(m => m.Day == currentDay);
                bool isOpened = (currentHour >= today.From && currentHour <= today.To);
                string openClose = isOpened ? "Now opened" : "Closed";
                string returnValue = $"{today.Day}: {today.Description} - {openClose}";

                return Json(new { Now = returnValue }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex) {
                Log(ex);
            }
            finally {
            }
            return null;
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
        #endregion Public Methods
    }
}