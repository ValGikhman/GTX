using GTX.Common;
using Services;
using System;
using System.Configuration;
using System.IO;
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
            return await DecodeInventoryReportAsync(vin, "dataone");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [OutputCache(NoStore = true, Duration = 0, VaryByParam = "*")]
        public async Task<ActionResult> DecodeMarketCheckByAnyVin(string vin)
        {
            return await DecodeInventoryReportAsync(vin, "neovin");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [OutputCache(NoStore = true, Duration = 0, VaryByParam = "*")]
        public ActionResult LoadSavedReport(string vin, string provider)
        {
            var normalizedVin = NormalizeVin(vin);
            if (normalizedVin == null || !VinDecoderReportClient.IsProvider(provider))
                return new HttpStatusCodeResult(400, "Select a VIN decoder and enter a valid VIN.");
            try
            {
                var stock = InventoryService.GetInventoryStockByVin(normalizedVin);
                if (stock == null) return HttpNotFound("This VIN is not in inventory.");
                return (ActionResult)ReadSavedReport(stock, normalizedVin, provider)
                    ?? HttpNotFound("No saved report exists for this VIN and provider.");
            }
            catch (VinDecoderReportException ex) { return new HttpStatusCodeResult(ex.StatusCode, ex.Message); }
            catch (Exception ex) when (ex is System.Data.SqlClient.SqlException || ex is InvalidOperationException || ex is System.Xml.XmlException || ex is ArgumentException)
            {
                return new HttpStatusCodeResult(503, "Saved VIN reports are temporarily unavailable.");
            }
        }

        private ContentResult ReadSavedReport(string stock, string vin, string provider)
        {
            var report = InventoryService.GetVinDecoderReport(stock, provider);
            if (report == null || (!string.IsNullOrWhiteSpace(report.Vin)
                && !string.Equals(report.Vin, vin, StringComparison.OrdinalIgnoreCase)))
                return null;
            var html = VinDecoderReportFormatter.Render(report);
            Response.AppendHeader("X-GTX-Vin-Saved", "true");
            Response.AppendHeader("X-GTX-Vin-Source", "database");
            return Content(html, "text/html", Encoding.UTF8);
        }

        private async Task<ActionResult> DecodeInventoryReportAsync(string vin, string provider)
        {
            var normalizedVin = NormalizeVin(vin);
            if (normalizedVin == null) return new HttpStatusCodeResult(400, "Enter a valid 17-character VIN.");
            string stock;
            try { stock = InventoryService.GetInventoryStockByVin(normalizedVin); }
            catch (Exception ex) when (ex is System.Data.SqlClient.SqlException || ex is InvalidOperationException)
            { return new HttpStatusCodeResult(503, "Inventory is temporarily unavailable."); }

            // Only a missing report may fall through to a provider request.
            if (stock != null)
            {
                try
                {
                    var saved = ReadSavedReport(stock, normalizedVin, provider);
                    if (saved != null) return saved;
                }
                catch (VinDecoderReportException ex) { return new HttpStatusCodeResult(ex.StatusCode, ex.Message); }
                catch (Exception ex) when (ex is System.Data.SqlClient.SqlException || ex is InvalidOperationException || ex is System.Xml.XmlException || ex is ArgumentException)
                { return new HttpStatusCodeResult(503, "Saved VIN reports are temporarily unavailable."); }
            }

            try
            {
                var apiKey = ServiceConfiguration.Get("AutoDealer:ApiKey");
                var configuredUrl = ServiceConfiguration.Get("AutoDealer:AppraisalBaseUrl");
                if (provider == "dataone")
                {
                    if (string.IsNullOrWhiteSpace(apiKey)) apiKey = "ad_live_demo.keysecret";
                    if (string.IsNullOrWhiteSpace(configuredUrl)) configuredUrl = "https://autodealer.dev/";
                }
                if (!Uri.TryCreate(configuredUrl, UriKind.Absolute, out var origin))
                    return new HttpStatusCodeResult(503, "Configure the vehicle research API URL and key in Services.");
                var report = await VinDecoderReportClient.FetchAsync(AutoDealerClient, origin, apiKey, normalizedVin, provider);
                report.Html = VinDecoderReportFormatter.Render(report);
                Response.AppendHeader("X-GTX-Vin-Source", "provider");
                if (stock == null) Response.AppendHeader("X-GTX-Vin-Saved", "false");
                else try
                {
                    InventoryService.SaveVinDecoderReport(stock, report);
                    Response.AppendHeader("X-GTX-Vin-Saved", "true");
                    AppCache.Remove(Constants.INVENTORY_CACHE);
                    AppCache.Remove(Constants.MAJORDOME_INVENTORY_CACHE);
                    AppCache.Remove(Constants.MAJORDOME_DASHBOARD_CACHE);
                    AppCache.RemoveByPrefix(Constants.CHAT_INVENTORY_CACHE);
                }
                catch (Exception ex) when (ex is System.Data.SqlClient.SqlException || ex is InvalidOperationException || ex is System.Xml.XmlException)
                {
                    Response.AppendHeader("X-GTX-Vin-Saved", "failed");
                }
                return Content(report.Html, "text/html", Encoding.UTF8);
            }
            catch (VinDecoderReportException ex) { return new HttpStatusCodeResult(ex.StatusCode, ex.Message); }
            catch (Exception ex) when (ex is ConfigurationErrorsException || ex is IOException || ex is UnauthorizedAccessException)
            { return new HttpStatusCodeResult(503, "The VIN decoding service configuration is unavailable."); }
        }

        internal static string NormalizeVin(string vin)
        {
            var normalizedVin = (vin ?? string.Empty).Trim().ToUpperInvariant();
            return Regex.IsMatch(normalizedVin, @"\A[A-HJ-NPR-Z0-9]{17}\z") ? normalizedVin : null;
        }

        internal static async Task<ActionResult> DecodeHtmlAsync(string vin)
        {
            var normalizedVin = NormalizeVin(vin);
            if (normalizedVin == null)
                return new HttpStatusCodeResult(400, "Enter a valid 17-character VIN.");

            // The demo credential is public; registered credentials belong in server configuration.
            var apiKey = ServiceConfiguration.Get("AutoDealer:ApiKey");
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

        internal static async Task<ActionResult> DecodeMarketCheckHtmlAsync(string vin)
        {
            var normalizedVin = NormalizeVin(vin);
            if (normalizedVin == null)
                return new HttpStatusCodeResult(400, "Enter a valid 17-character VIN.");

            string apiKey;
            string configuredUrl;
            try
            {
                apiKey = ServiceConfiguration.Get("AutoDealer:ApiKey");
                configuredUrl = ServiceConfiguration.Get("AutoDealer:AppraisalBaseUrl");
            }
            catch (Exception ex) when (ex is ConfigurationErrorsException || ex is IOException || ex is UnauthorizedAccessException)
            {
                return new HttpStatusCodeResult(503, "The VIN decoding service configuration is unavailable.");
            }

            if (string.IsNullOrWhiteSpace(apiKey) || !Uri.TryCreate(configuredUrl, UriKind.Absolute, out var origin)
                || origin.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(origin.UserInfo)
                || !string.IsNullOrEmpty(origin.Query) || !string.IsNullOrEmpty(origin.Fragment))
                return new HttpStatusCodeResult(503, "Configure the vehicle research API URL and key in Services to use MarketCheck NeoVIN.");

            try
            {
                return await FetchMarketCheckHtmlAsync(AutoDealerClient, normalizedVin, apiKey.Trim(), origin);
            }
            catch (OperationCanceledException)
            {
                return new HttpStatusCodeResult(504, "VIN decoding timed out. Please try again.");
            }
            catch (HttpRequestException)
            {
                return new HttpStatusCodeResult(502, "The VIN decoding service is temporarily unavailable.");
            }
        }

        private static Task<ActionResult> FetchAutoDealerHtmlAsync(HttpClient client, string vin, string apiKey)
        {
            return FetchDecoderHtmlAsync(client, new Uri("https://autodealer.dev/api/service/vin/" + Uri.EscapeDataString(vin) + "/html"), apiKey);
        }

        private static Task<ActionResult> FetchMarketCheckHtmlAsync(HttpClient client, string vin, string apiKey, Uri origin)
        {
            return FetchDecoderHtmlAsync(client, new Uri(origin, "/api/service/neovin/" + Uri.EscapeDataString(vin) + "/html"), apiKey);
        }

        private static async Task<ActionResult> FetchDecoderHtmlAsync(HttpClient client, Uri endpoint, string apiKey)
        {
            using (var request = new HttpRequestMessage(HttpMethod.Get, endpoint))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/html"));
                using (var response = await client.SendAsync(request))
                {
                    if (response.StatusCode == (HttpStatusCode)429)
                        return new HttpStatusCodeResult(429, "The VIN decoding limit has been reached.");
                    if (!response.IsSuccessStatusCode)
                        return new HttpStatusCodeResult(502, "The VIN decoding service could not complete the request.");

                    if (response.Content == null)
                        return new HttpStatusCodeResult(502, "The VIN decoding service returned an invalid response.");
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
