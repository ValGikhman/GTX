﻿using GTX.Models;
using Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Web.Mvc;

namespace GTX.Controllers {

    public abstract class BaseController : Controller {

        #region Properties
        public readonly String devComputer = "VALS-PC";

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
                Model = new BaseModel();
                Model.IsDevelopment = (Environment.GetEnvironmentVariable("COMPUTERNAME") == devComputer);
                var session = filterContext.HttpContext.Session;

                SessionData.SetSession(Constants.SESSION_ENVIRONMENT, Model.IsDevelopment ? "Development": "Production");
                ViewBag.Environment = SessionData.Environment;

                if (SessionData == null || SessionData?.Inventory == null) {
                    Model.Inventory = await SetModel(Model.Inventory);
                    SessionData.SetSession(Constants.SESSION_INVENTORY, Model.Inventory);
                }
                Model.Inventory = SessionData.Inventory;

                if (SessionData?.Employers == null) {
                    Employer[] employers = await Utility.XMLHelpers.XmlRepository.GetEmployers();
                    SessionData.SetSession(Constants.SESSION_EMPLOYERS, employers);
                }

                Model.Employers = SessionData.Employers;

                if (SessionData?.Filters == null) {
                    Filters filters = new Filters();
                    filters.Makes = Model.Inventory.All.Select(m => m.Make).Distinct().OrderBy(m => m).ToArray();
                    filters.Models = Model.Inventory.All.Select(m => m.Model).Distinct().OrderBy(m => m).ToArray();
                    filters.Engines = Model.Inventory.All.Select(m => m.Engine).Distinct().OrderBy(m => m).ToArray();
                    filters.FuelTypes = Model.Inventory.All.Select(m => m.FuelType).Distinct().OrderBy(m => m).ToArray();
                    filters.MaxPrice = Model.Inventory.All.Max(m => m.RetailPrice);
                    filters.MinPrice = Model.Inventory.All.Min(m => m.RetailPrice);
                    filters.DriveTrains = Model.Inventory.All.Select(m => m.DriveTrain).Distinct().OrderBy(m => m).ToArray();
                    filters.BodyTypes = Model.Inventory.All.Select(m => m.Body).Distinct().OrderBy(m => m).ToArray();
                    filters.VehicleTypes = Model.Inventory.All.Select(m => m.VehicleType).Distinct().OrderBy(m => m).ToArray();
                    SessionData.SetSession(Constants.SESSION_FILTERS, filters);
                }

                if (SessionData?.OpenHours == null) {
                    OpenHours[] openHours = Utility.XMLHelpers.XmlRepository.GetOpenHours();
                    SessionData.SetSession(Constants.SESSION_OOPEN_HOURS, openHours);
                    Model.OpenHours = openHours;                
                }

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
                model.All = await Utility.XMLHelpers.XmlRepository.GetInventory();
                model.All = model.All.Where(m => m.RetailPrice > 0).OrderByDescending(m => m.PurchaseDate).ThenBy(m => m.Make).ToArray();
                model.All = ApplyImages(model.All);
                model.Vehicles = model.All;

                var carTypes = new HashSet<string> {
                    CommonUnit.VehicleType.SEDAN.ToString(),
                    CommonUnit.VehicleType.COUPE.ToString(),
                    CommonUnit.VehicleType.CONVERTIBLE.ToString(),
                    CommonUnit.VehicleType.HATCHBACK.ToString(),
                    CommonUnit.VehicleType.WAGON.ToString(),
                };

                model.Cars = model.All.Where(m => carTypes.Contains(m.VehicleType.ToUpper())).ToArray();
                model.Suvs = model.All.Where(m => m.VehicleType.ToUpper().Equals(CommonUnit.VehicleType.SUV.ToString())).ToArray();
                model.Trucks = model.All.Where(m => m.VehicleType.ToUpper().Equals(CommonUnit.VehicleType.TRUCK.ToString())).ToArray();
                model.Vans = model.All.Where(m => m.VehicleType.ToUpper().Equals(CommonUnit.VehicleType.VAN.ToString())).ToArray();
                model.Hatchbacks = model.All.Where(m => m.VehicleType.ToUpper().Equals(CommonUnit.VehicleType.HATCHBACK.ToString())).ToArray();
                model.Convertibles = model.All.Where(m => m.VehicleType.ToUpper().Equals(CommonUnit.VehicleType.CONVERTIBLE.ToString())).ToArray();
                model.Sedans = model.All.Where(m => m.VehicleType.ToUpper().Equals(CommonUnit.VehicleType.SEDAN.ToString())).ToArray();
                model.Coupe = model.All.Where(m => m.VehicleType.ToUpper().Equals(CommonUnit.VehicleType.COUPE.ToString())).ToArray();
                model.Wagons = model.All.Where(m => m.VehicleType.ToUpper().Equals(CommonUnit.VehicleType.WAGON.ToString())).ToArray();
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
                if (today.From == 0 && today.To == 0) {
                    returnValue = $"{today.Day}: {today.Description}";
                }
                else {
                    returnValue = $"{today.Day}: {today.Description} - {openClose}";
                }

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

        public Models.GTX[] ApplyImages(Models.GTX[] vehicles) {
            string path = @"/GTXImages/Inventory/";
            foreach (var vehicle in vehicles) {
                string dirPath = Server.MapPath($"{path}{vehicle.Stock}");
                vehicle.Image = $"{path}no-image.png";

                if (Directory.Exists(dirPath)) {
                    vehicle.Images = Directory.GetFiles(dirPath)
                        .Where(f => f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                                    f.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                                    f.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase))
                        .Select(f => $"{path}{vehicle.Stock}/{Path.GetFileName(f)}")
                        .ToArray();

                    if (vehicle.Images != null && vehicle.Images.Length > 0) {
                        vehicle.Image = vehicle.Images[0];
                    }
                }
            }
            return vehicles;
        }
        #endregion Public Methods
    }
}