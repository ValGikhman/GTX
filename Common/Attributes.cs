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

        // If you want to override role name later
        public CommonUnit.Roles RequiredRole { get; set; } = CommonUnit.Roles.User;

        public override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            if (filterContext == null) return;

            // Don’t break child actions, ajax, etc. (optional rules)
            if (filterContext.IsChildAction) return;

            var role = RoleCookie.GetCurrentRole(
                filterContext.HttpContext.Request,
                filterContext.HttpContext.Session
            );

            // If not required role => redirect to your “login info” page
            if (role == RequiredRole)
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
