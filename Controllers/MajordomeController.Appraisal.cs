using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Configuration;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;

namespace GTX.Controllers
{
    public partial class MajordomeController
    {
        private static readonly HttpClient AppraisalClient = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false })
        {
            Timeout = TimeSpan.FromSeconds(70)
        };

        [HttpGet]
        public ActionResult Appraisal()
        {
            if (!CanUseAppraisal()) return new HttpStatusCodeResult(403);
            Response.Cache.SetNoStore();
            return View();
        }

        [HttpGet]
        public ActionResult Apprisal()
        {
            return RedirectToAction(nameof(Appraisal));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public Task<ActionResult> AppraisalResearch(string vin, int? miles, string zip, int? radius)
        {
            var payload = new JObject { ["vin"] = vin, ["miles"] = miles };
            if (!string.IsNullOrWhiteSpace(zip)) payload["zip"] = zip.Trim();
            if (radius.HasValue) payload["radius"] = radius.Value;
            return SendAppraisalAsync("", payload);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public Task<ActionResult> AppraisalBid(decimal? expectedSellingPriceUsd, decimal? currentHammerBidUsd)
        {
            return SendAppraisalAsync("/auction-budget", new { expectedSellingPriceUsd, currentHammerBidUsd });
        }

        private bool CanUseAppraisal()
        {
            var role = RoleCookie.GetCurrentRole(Request, Session);
            return role == CommonUnit.Roles.Owner || role == CommonUnit.Roles.Manager;
        }

        private async Task<ActionResult> SendAppraisalAsync(string routeSuffix, object payload)
        {
            if (!CanUseAppraisal()) return AppraisalJson(403, new { message = "Sign in as an Owner or Manager to use appraisal." });
            if (!ModelState.IsValid) return AppraisalJson(400, new { message = "Check the values and try again." });

            var key = ConfigurationManager.AppSettings["AutoDealer:ApiKey"]?.Trim();
            var configuredUrl = ConfigurationManager.AppSettings["AutoDealer:AppraisalBaseUrl"];
            if (string.IsNullOrWhiteSpace(key) || !Uri.TryCreate(configuredUrl, UriKind.Absolute, out var origin)
                || origin.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(origin.UserInfo)
                || !string.IsNullOrEmpty(origin.Query) || !string.IsNullOrEmpty(origin.Fragment))
                return AppraisalJson(503, new { message = "AutoDealer.dev appraisal is not configured." });

            try
            {
                var url = new Uri(origin, "/api/service/price-research" + routeSuffix);
                using (var request = new HttpRequestMessage(HttpMethod.Post, url))
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
                    request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                    request.Content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");
                    using (var response = await AppraisalClient.SendAsync(request))
                    {
                        if (!response.IsSuccessStatusCode)
                        {
                            switch (response.StatusCode)
                            {
                                case HttpStatusCode.BadRequest:
                                    return AppraisalJson(400, new { message = "Check your appraisal inputs and try again." });
                                case HttpStatusCode.Unauthorized:
                                case HttpStatusCode.Forbidden:
                                    return AppraisalJson(502, new { message = "AutoDealer.dev rejected the configured API key or subscription." });
                                case (HttpStatusCode)429:
                                    return AppraisalJson(429, new { message = "The AutoDealer.dev request limit has been reached." });
                                case HttpStatusCode.GatewayTimeout:
                                    return AppraisalJson(504, new { message = "The appraisal request timed out. Please try again." });
                                default:
                                    return AppraisalJson(502, new { message = "AutoDealer.dev could not complete this appraisal request." });
                            }
                        }

                        var result = JObject.Parse(await response.Content.ReadAsStringAsync());
                        if (routeSuffix.Length == 0)
                            return AppraisalJson(200, new { data = result["data"], demand = result["demand"], request = result["request"] });
                        return AppraisalJson(200, result);
                    }
                }
            }
            catch (TaskCanceledException)
            {
                return AppraisalJson(504, new { message = "The appraisal request timed out. Please try again." });
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is JsonException)
            {
                return AppraisalJson(502, new { message = "The AutoDealer.dev appraisal service could not be reached." });
            }
        }

        private ActionResult AppraisalJson(int status, object value)
        {
            Response.Cache.SetCacheability(HttpCacheability.NoCache);
            Response.Cache.SetNoStore();
            Response.StatusCode = status;
            Response.TrySkipIisCustomErrors = true;
            return Content(JsonConvert.SerializeObject(value), "application/json", Encoding.UTF8);
        }
    }
}
