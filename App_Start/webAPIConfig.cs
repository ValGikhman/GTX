using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http;

namespace GTX.Api {
    public static class WebApiConfig {
        public static void Register(HttpConfiguration config) {
            config.Formatters.JsonFormatter.SerializerSettings.ContractResolver =
                new CamelCasePropertyNamesContractResolver();
            config.Formatters.JsonFormatter.SerializerSettings.NullValueHandling = NullValueHandling.Ignore;
            config.Formatters.Remove(config.Formatters.XmlFormatter);

            // Native apps do not require CORS, but the Expo web build does.
            // Keep this handler limited to the public, GET-only mobile API.
            config.MessageHandlers.Add(new MobileApiCorsHandler());

            config.MapHttpAttributeRoutes();

            config.Routes.MapHttpRoute(
                name: "DefaultApi",
                routeTemplate: "api/{controller}/{id}",
                defaults: new { id = RouteParameter.Optional }
            );
        }
    }

    internal sealed class MobileApiCorsHandler : DelegatingHandler {
        private const string MobileApiPath = "/api/mobile/v1/";

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) {

            var path = request?.RequestUri?.AbsolutePath ?? string.Empty;
            if (!path.StartsWith(MobileApiPath, StringComparison.OrdinalIgnoreCase)) {
                return await base.SendAsync(request, cancellationToken);
            }

            HttpResponseMessage response;
            if (request.Method == HttpMethod.Options) {
                response = new HttpResponseMessage(HttpStatusCode.OK) { RequestMessage = request };
            }
            else {
                response = await base.SendAsync(request, cancellationToken);
            }

            response.Headers.TryAddWithoutValidation("Access-Control-Allow-Origin", "*");
            response.Headers.TryAddWithoutValidation("Access-Control-Allow-Methods", "GET, OPTIONS");
            response.Headers.TryAddWithoutValidation("Access-Control-Allow-Headers", "Accept, Content-Type");
            return response;
        }
    }
}
