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
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;

namespace GTX.Controllers
{
    public partial class MajordomeController
    {
        public static string AppraisalDefaultZip => SiteProfileStore.Current.ZipCode;
        public static string AppraisalDefaultRadius => SiteProfileStore.Current.ResearchRadiusMiles.ToString(System.Globalization.CultureInfo.InvariantCulture);

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

        [HttpPost, AsyncTimeout(150000)]
        public Task<ActionResult> AppraisalResearch(string vin, int? miles, string zip, int? radius)
        {
            var payload = new JObject { ["vin"] = vin, ["miles"] = miles };
            payload["zip"] = string.IsNullOrWhiteSpace(zip) ? AppraisalDefaultZip : zip.Trim();
            payload["radius"] = radius ?? SiteProfileStore.Current.ResearchRadiusMiles;
            return SendAppraisalAsync("", payload);
        }

        [HttpPost]
        public Task<ActionResult> AppraisalBid(decimal? expectedSellingPriceUsd, decimal? currentHammerBidUsd)
        {
            return SendAppraisalAsync("/auction-budget", new { expectedSellingPriceUsd, currentHammerBidUsd });
        }

        private bool CanUseAppraisal()
        {
            return SiteAccess.Can(SiteAccess.Current(Request), MajordomeItem.Appraisal);
        }

        private async Task<ActionResult> SendAppraisalAsync(string routeSuffix, object payload, HttpMethod method = null)
        {
            if (!CanUseAppraisal()) return AppraisalJson(403, new { message = "Sign in with a role that has Appraisal access." });
            try
            {
                System.Web.Helpers.AntiForgery.Validate();
            }
            catch (HttpAntiForgeryException)
            {
                return AppraisalJson(403, new { message = "The appraisal form could not be verified. Refresh the page and try again." });
            }
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
                using (var request = new HttpRequestMessage(method ?? HttpMethod.Post, url))
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
                    request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                    request.Headers.UserAgent.ParseAdd("GTXAutoGroup/1.0");
                    if (payload != null)
                    {
                        request.Headers.ExpectContinue = false;
                        request.Content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");
                    }
                    using (var response = await AppraisalClient.SendAsync(request))
                    {
                        if (!response.IsSuccessStatusCode)
                        {
                            Response.AppendHeader("X-GTX-Appraisal-Error", "autodealer-http-" + (int)response.StatusCode);
                            switch (response.StatusCode)
                            {
                                case HttpStatusCode.BadRequest:
                                    return AppraisalJson(400, new { message = "Check your appraisal inputs and try again." });
                                case HttpStatusCode.Unauthorized:
                                case HttpStatusCode.Forbidden:
                                    return await AppraisalRejectionAsync(response);
                                case (HttpStatusCode)429:
                                    return AppraisalJson(429, new { message = "The AutoDealer.dev request limit has been reached." });
                                case HttpStatusCode.ServiceUnavailable:
                                    return AppraisalJson(503, new { message = "AutoDealer.dev reports that appraisal is unavailable. Check the MarketCheck configuration on AutoDealer.dev." });
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
                Response.AppendHeader("X-GTX-Appraisal-Error", "autodealer-timeout");
                return AppraisalJson(504, new { message = "The appraisal request timed out. Please try again." });
            }
            catch (HttpRequestException ex)
            {
                var cause = ex.InnerException as WebException ?? ex.GetBaseException() as WebException;
                Response.AppendHeader("X-GTX-Appraisal-Error", cause == null ? "autodealer-transport" : "autodealer-transport-" + cause.Status);
                return AppraisalJson(502, new { message = "The AutoDealer.dev appraisal service could not be reached." });
            }
            catch (JsonException)
            {
                Response.AppendHeader("X-GTX-Appraisal-Error", "autodealer-invalid-json");
                return AppraisalJson(502, new { message = "AutoDealer.dev returned an unreadable appraisal response." });
            }
        }

        private async Task<ActionResult> AppraisalRejectionAsync(HttpResponseMessage response)
        {
            string ray = null;
            if (response.Headers.TryGetValues("CF-Ray", out var rays))
            {
                ray = rays.FirstOrDefault(value => Regex.IsMatch(value, @"\A[0-9a-fA-F]{16}(?:-[A-Z]{3})?\z"));
                if (ray != null)
                    Response.AppendHeader("X-GTX-Appraisal-Upstream-Ray", ray);
            }

            var mediaType = response.Content?.Headers.ContentType?.MediaType;
            string code = null;
            if (string.Equals(mediaType, "application/json", StringComparison.OrdinalIgnoreCase)
                || string.Equals(mediaType, "application/problem+json", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var body = JObject.Parse(await response.Content.ReadAsStringAsync());
                    var token = (body["error"] as JObject)?["code"] ?? body["error_name"];
                    if (token?.Type == JTokenType.String && Regex.IsMatch((string)token, @"\A[a-z0-9_]{1,64}\z"))
                        code = (string)token;
                }
                catch (JsonException)
                {
                    // Keep the status visible even if the upstream error body is unreadable.
                }
            }

            if (code != null) Response.AppendHeader("X-GTX-Appraisal-Upstream-Code", code);
            var responseType = string.Equals(mediaType, "text/html", StringComparison.OrdinalIgnoreCase) ? "HTML response"
                : "no API error code";
            var details = code ?? responseType;
            if (ray != null) details += ", Ray ID " + ray;

            // Cloudflare can replace a 502 response and hide our diagnostic message.
            return AppraisalJson(424, new { message = $"AutoDealer.dev denied the appraisal request (HTTP {(int)response.StatusCode}, {details})." });
        }

        private static async Task<JObject> GetAppraisalReferencesAsync(Uri origin, string key, JObject data, JObject scope)
        {
            var current = (data["comparables"] as JObject)?["listings"] as JArray ?? new JArray();
            var recent = (data["recent_comparables"] as JObject)?["listings"] as JArray ?? new JArray();
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
                        request.Headers.UserAgent.ParseAdd("GTXAutoGroup/1.0");
                        request.Headers.ExpectContinue = false;
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
