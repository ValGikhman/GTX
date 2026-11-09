using GTX.Common;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Configuration;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;

namespace GTX.Controllers
{
    public partial class MajordomeController
    {
        public const string AppraisalDefaultZip = "45040";
        public const string AppraisalDefaultRadius = "100";

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

        [HttpPost, ValidateAntiForgeryToken, AsyncTimeout(150000)]
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
            return SiteAccess.Can(SiteAccess.Current(Request), MajordomeItem.Appraisal);
        }

        private async Task<ActionResult> SendAppraisalAsync(string routeSuffix, object payload)
        {
            if (!CanUseAppraisal()) return AppraisalJson(403, new { message = "Sign in with a role that has Appraisal access." });
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
                        {
                            var data = result["data"] as JObject;
                            var scope = result["request"] as JObject;
                            if (data != null && scope != null) {
                                data["marketCheckReferences"] = await GetAppraisalReferencesAsync(origin, key, data, scope);
                            }
                            return AppraisalJson(200, new { data, demand = result["demand"], request = scope });
                        }
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

        private static async Task<JObject> GetAppraisalReferencesAsync(Uri origin, string key, JObject data, JObject scope)
        {
            var current = data["comparables"]?["listings"] as JArray ?? new JArray();
            var recent = data["recent_comparables"]?["listings"] as JArray ?? new JArray();
            var selectedCurrent = AppraisalComparableScope.Select(current, scope, "MarketCheck Current");
            var selectedRecent = AppraisalComparableScope.Select(recent, scope, "MarketCheck Recent", 100);
            JObject expanded = null;
            if ((int)selectedCurrent["strictCount"] < 5 || (int)selectedRecent["strictCount"] < 5)
            {
                try
                {
                    using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30)))
                    using (var request = new HttpRequestMessage(HttpMethod.Post, new Uri(origin, "/api/service/price-research/auction-comparables")))
                    {
                        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
                        request.Content = new StringContent(scope.ToString(Formatting.None), Encoding.UTF8, "application/json");
                        using (var response = await AppraisalClient.SendAsync(request, timeout.Token))
                            if (response.IsSuccessStatusCode)
                                expanded = JObject.Parse(await response.Content.ReadAsStringAsync());
                    }
                }
                catch (Exception ex) when (ex is HttpRequestException || ex is OperationCanceledException || ex is JsonException)
                {
                    // Original results remain usable when a wider search cannot complete.
                }
            }
            if (expanded != null)
            {
                selectedCurrent = AppraisalComparableScope.Select(new JArray(current.Concat(expanded["current"] as JArray ?? new JArray())), scope, "MarketCheck Current", (int?)expanded["currentRadiusLimit"] ?? 5000);
                selectedRecent = AppraisalComparableScope.Select(new JArray(recent.Concat(expanded["recent"] as JArray ?? new JArray())), scope, "MarketCheck Recent", 100);
            }
            else if ((int)selectedCurrent["strictCount"] < 5 || (int)selectedRecent["strictCount"] < 5)
            {
                selectedCurrent["note"] = (string)selectedCurrent["note"] + " The additional MarketCheck lookup was unavailable; showing the original provider results.";
                selectedRecent["note"] = (string)selectedRecent["note"] + " The additional MarketCheck lookup was unavailable; showing the original provider results.";
            }
            return new JObject
            {
                ["vehicle"] = expanded?["vehicle"], ["current"] = selectedCurrent, ["recent"] = selectedRecent
            };
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
