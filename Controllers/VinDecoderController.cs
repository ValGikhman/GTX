using GTX.Common;
using Services;
using System;
using System.Configuration;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.Mvc;

namespace GTX.Controllers
{
    [RequireAdminRole]
    public class VinDecoderController : BaseController
    {
        private static readonly HttpClient AutoDealerClient = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false })
        {
            Timeout = TimeSpan.FromSeconds(60)
        };

        public VinDecoderController(ISessionData sessionData, IInventoryService inventoryService, IVinDecoderService vinDecoderService, ILogService logService, IEmployeesService employeesService)
        : base(sessionData, inventoryService, vinDecoderService, logService, employeesService) {}

        public ActionResult Index()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [OutputCache(NoStore = true, Duration = 0, VaryByParam = "*")]
        public async Task<ActionResult> DecodeDataOneByAnyVin(string vin)
        {
            var normalizedVin = (vin ?? string.Empty).Trim().ToUpperInvariant();
            if (!Regex.IsMatch(normalizedVin, @"^[A-HJ-NPR-Z0-9]{17}$"))
                return new HttpStatusCodeResult(400, "Enter a valid 17-character VIN.");

            // The demo credential is public; registered credentials belong in server configuration.
            var apiKey = ConfigurationManager.AppSettings["AutoDealer:ApiKey"];
            if (string.IsNullOrWhiteSpace(apiKey)) apiKey = "ad_live_demo.keysecret";

            try
            {
                return await FetchAutoDealerHtmlAsync(AutoDealerClient, normalizedVin, apiKey.Trim());
            }
            catch (TaskCanceledException)
            {
                return new HttpStatusCodeResult(504, "VIN decoding timed out. Please try again.");
            }
            catch (HttpRequestException)
            {
                return new HttpStatusCodeResult(502, "The VIN decoding service is temporarily unavailable.");
            }
        }

        private static async Task<ActionResult> FetchAutoDealerHtmlAsync(HttpClient client, string vin, string apiKey)
        {
            using (var request = new HttpRequestMessage(HttpMethod.Get,
                "https://autodealer.dev/api/service/vin/" + Uri.EscapeDataString(vin) + "/html"))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/html"));
                using (var response = await client.SendAsync(request))
                {
                    if (response.StatusCode == (HttpStatusCode)429)
                        return new HttpStatusCodeResult(429, "The VIN decoding limit has been reached.");
                    if (!response.IsSuccessStatusCode)
                        return new HttpStatusCodeResult(502, "The VIN decoding service could not complete the request.");

                    var html = await response.Content.ReadAsStringAsync();
                    if (!string.Equals(response.Content.Headers.ContentType?.MediaType, "text/html", StringComparison.OrdinalIgnoreCase)
                        || string.IsNullOrWhiteSpace(html))
                        return new HttpStatusCodeResult(502, "The VIN decoding service returned an invalid response.");

                    return new ContentResult { Content = html, ContentType = "text/html", ContentEncoding = Encoding.UTF8 };
                }
            }
        }
    }
}
