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

namespace GTX.Controllers {

    public abstract class BaseController : Controller {

        #region Properties

        public ILogService _LogService { set; get; }

        public ISessionData SessionData { get; private set; }

        public BaseModel Model { get; set; }

        #endregion Properties

        #region Construtors

        public BaseController(ISessionData sessionData) {
            SessionData = sessionData;
            _LogService = new LogService();
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
                model.Inventory = await SetModel(model.Inventory);
                SessionData.SetSession(Constants.SESSION_INVENTORY, model.Inventory);

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

        public void Log(CommonUnit.LogType logType) {
            LogData(logType);
        }

        public void Log(CommonUnit.LogType logType, String trace) {
            LogData(logType, trace);
        }

        public void Log(Exception exception) {
            LogData(exception);
        }

        public void Log(CommonUnit.LogType type, String trace, String route) {
            LogData(type, trace, route);
        }

        private void LogAcvitity(ActionExecutingContext filterContext) {
            //Log(CommonUnit.LogType.Activity
            //    , "Navigating"
            //    , SessionData.route
            //);
        }

        private void LogData(CommonUnit.LogType logType) {
            try {
                //_LogService.Log(logType, SessionData.user.id, SessionData.sessionId, EnumHelper<CommonUnit.LogType>.Parse(logType.ToString()).ToString());
            }
            catch (Exception ex) {
                throw ex;
            }
        }

        private void LogData(CommonUnit.LogType logType, String trace) {
            try {
                //_LogService.Log(logType, SessionData.user.id, SessionData.sessionId, trace);
            }
            catch (Exception ex) {
                throw ex;
            }
        }

        private void LogData(Exception exception) {
            try {
                //_LogService.Log(CommonUnit.LogType.Exception, SessionData.user.id, SessionData.sessionId);
            }
            catch (Exception ex) {
                throw ex;
            }
        }

        private void LogData(CommonUnit.LogType logType, String trace, String route) {
            try {
                //_LogService.Log(logType, SessionData.user.id, SessionData.sessionId, trace, route);
            }
            catch (Exception ex) {
                throw ex;
            }
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

                model.Cars = model.All.Where(m => m.Body.Equals("2DR")).ToArray();
                model.Suvs = model.All.Where(m => m.Body.Equals("3DR")).ToArray();
                model.Trucks = model.All.Where(m => m.Body.Equals("5DR")).ToArray();
                model.Vans = model.All.Where(m => m.Body.Equals("5DR")).ToArray();
                model.Cargo = model.All.Where(m => m.Body.Equals("5DR")).ToArray();
                SessionData.SetSession(Constants.SESSION_INVENTORY, model);

                return model;
            }

            return SessionData.Inventory;
        }

        #endregion Public Methods
    }
}