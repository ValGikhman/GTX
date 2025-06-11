using System.Web;

namespace GTX {
    public class HttpContextProvider: IHttpContextProvider  {
        public HttpContextBase Current {
            get {
                return new HttpContextWrapper(HttpContext.Current);
            }
        }
    }
}