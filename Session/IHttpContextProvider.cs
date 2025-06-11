using System.Web;

namespace GTX {
    public class IHttpContextProvider {
        /// <summary>
        /// Gets the current HTTP context.
        /// </summary>
        /// <value>The current HTTP context.</value>
        HttpContextBase Current { get; }
    }
}