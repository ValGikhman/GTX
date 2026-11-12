using System;
using System.Web;
using System.Web.Mvc;

namespace GTX.Common
{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
    public sealed class RequireAdminRole : ActionFilterAttribute
    {
        // Where to send people if they are not "User"
        public string RedirectController { get; set; } = "Home";
        public string RedirectAction { get; set; } = "NeedLogin";

        public bool RequireMajordome { get; set; }

        public override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            if (filterContext == null) return;

            // Don’t break child actions, ajax, etc. (optional rules)
            if (filterContext.IsChildAction) return;

            if (filterContext.ActionDescriptor.IsDefined(typeof(AllowAnonymousAttribute), true) ||
                filterContext.ActionDescriptor.ControllerDescriptor.IsDefined(typeof(AllowAnonymousAttribute), true))
            {
                base.OnActionExecuting(filterContext);
                return;
            }

            var role = SiteAccess.Current(filterContext.HttpContext.Request);
            var controller = filterContext.ActionDescriptor.ControllerDescriptor.ControllerName;
            var item = MajordomeMenu.ForRequest(controller, filterContext.ActionDescriptor.ActionName);
            var hasAccess = SiteAccess.Can(role, item);
            if (RequireMajordome) hasAccess = hasAccess && SiteAccess.IsMajordome(role);

            if (!hasAccess)
            {
                // Preserve return url
                var url = filterContext.HttpContext?.Request?.RawUrl ?? "/";
                filterContext.Result = new RedirectToRouteResult(
                    new System.Web.Routing.RouteValueDictionary(new
                    {
                        controller = RedirectController,
                        action = RedirectAction,
                        returnUrl = url
                    })
                );
                return;
            }

            base.OnActionExecuting(filterContext);
        }
    }
}
