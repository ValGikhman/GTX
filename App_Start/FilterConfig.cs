using System.Text;
using System.Web.Mvc;

namespace GTX {
    public sealed class Utf8ResponseEncodingFilter : ActionFilterAttribute {
        public override void OnResultExecuting(ResultExecutingContext filterContext) {
            if (filterContext == null || filterContext.Result is FileResult) return;

            var response = filterContext.HttpContext?.Response;
            if (response == null) return;

            response.ContentEncoding = Encoding.UTF8;
            response.HeaderEncoding = Encoding.UTF8;
            response.Charset = "utf-8";

            var contentResult = filterContext.Result as ContentResult;
            if (contentResult != null && contentResult.ContentEncoding == null) {
                contentResult.ContentEncoding = Encoding.UTF8;
            }

            var jsonResult = filterContext.Result as JsonResult;
            if (jsonResult != null && jsonResult.ContentEncoding == null) {
                jsonResult.ContentEncoding = Encoding.UTF8;
            }
        }
    }

    public class FilterConfig {
        public static void RegisterGlobalFilters(GlobalFilterCollection filters) {
            filters.Add(new HandleErrorAttribute());
            filters.Add(new Utf8ResponseEncodingFilter());
        }
    }
}
