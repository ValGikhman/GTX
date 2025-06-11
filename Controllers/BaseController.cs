using GTX.Session;
using Services;
using System;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Web.Mvc;
using System.Web.Routing;
using System.Threading.Tasks;

namespace GTX.Controllers {

    public abstract class BaseController : Controller {

        #region Properties

        public ILogService _LogService { set; get; }

        public Boolean IsLoggedIn { set; get; }

        public ISessionData SessionData { get; private set; }

        #endregion Properties

        #region Construtors

        public BaseController(ISessionData sessionData) {
            SessionData = sessionData;
            _LogService = new LogService();

        }

        #endregion Construtors

        #region Overrides

        protected override void Initialize(System.Web.Routing.RequestContext requestContext) {
            base.Initialize(requestContext);
        }

        protected override void OnActionExecuting(ActionExecutingContext filterContext) {
            MvcHandler handler;
            String route = null;
            String culture = null;

            base.OnActionExecuting(filterContext);

            try {
                // Get the route
                handler = this.HttpContext.Handler as MvcHandler;
                if (handler != null && handler.RequestContext != null && handler.RequestContext.RouteData != null) {
                    // Route
                    if (handler.RequestContext.RouteData.Values != null && handler.RequestContext.RouteData.Route != null) {
                        route = handler.RequestContext.RouteData.Route.GetVirtualPath(handler.RequestContext, handler.RequestContext.RouteData.Values).VirtualPath;
                    }
                }

                if (Request.Cookies["language"] != null) {
                    culture = Server.HtmlEncode(Request.Cookies["language"].Value);
                }

                if (!String.IsNullOrEmpty(culture)) {
                    Thread.CurrentThread.CurrentUICulture = new CultureInfo(culture);
                }
            }
            catch (Exception ex) {
                throw ex;
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

        private String BuildRoute() {
            String retValue = String.Empty;
            if (RouteData.Values["controller"] != null)
                retValue = RouteData.Values["controller"].ToString();

            if (RouteData.Values["action"] != null)
                retValue = String.Format("{0}/{1}", retValue, RouteData.Values["action"].ToString());

            if (RouteData.Values["id"] != null)
                retValue = String.Format("{0}/{1}", retValue, RouteData.Values["id"].ToString());

            return retValue;
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

        #endregion Public Methods
    }
}