using GTX.Common;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;

namespace GTX.Controllers {
    public partial class MajordomeController {
        [HttpPost, ValidateAntiForgeryToken, AsyncTimeout(45000)]
        public async Task<ActionResult> AppraisalDecodeVehicle(string vin) {
            Response.Cache.SetNoStore();
            if (!CanUseAppraisal()) return AppraisalJson(403, new { message = "Sign in with a role that has Appraisal access." });
            var normalizedVin = (vin ?? "").Trim().ToUpperInvariant();
            if (!Regex.IsMatch(normalizedVin, "^[A-HJ-NPR-Z0-9]{17}$"))
                return AppraisalJson(400, new { message = "Enter a valid 17-character VIN." });

            var key = ServiceConfiguration.Get("AutoDealer:ApiKey")?.Trim();
            if (string.IsNullOrWhiteSpace(key)) key = "ad_live_demo.keysecret";
            try {
                using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30)))
                using (var request = new HttpRequestMessage(HttpMethod.Get,
                    "https://autodealer.dev/api/service/vin/" + Uri.EscapeDataString(normalizedVin) + "/json")) {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
                    request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                    using (var response = await AppraisalClient.SendAsync(request, timeout.Token)) {
                        if ((int)response.StatusCode == 429)
                            return AppraisalJson(429, new { message = "The VIN decoding limit has been reached. Try again later." });
                        if (!response.IsSuccessStatusCode)
                            return AppraisalJson(502, new { message = "The VIN decoding service could not complete the request." });
                        var decoded = JObject.Parse(await response.Content.ReadAsStringAsync());
                        try {
                            return AppraisalJson(200, new { vehicle = AppraisalVehicleIdentity.FromAutoDealerJson(normalizedVin, decoded) });
                        }
                        catch (InvalidOperationException) {
                            return AppraisalJson(422, new { message = "The VIN decoder did not identify a single vehicle. Try again or check the VIN." });
                        }
                    }
                }
            }
            catch (OperationCanceledException) {
                return AppraisalJson(504, new { message = "VIN decoding timed out. Please try again." });
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is JsonException) {
                return AppraisalJson(502, new { message = "The VIN decoding service is temporarily unavailable." });
            }
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult AppraisalParseFile(HttpPostedFileBase file) {
            if (!CanUseAppraisal()) return new HttpStatusCodeResult(403);
            Response.Cache.SetNoStore();
            if (file == null || file.ContentLength == 0 || file.ContentLength > 10 * 1024 * 1024 || !new[] { ".pdf", ".csv" }.Contains((Path.GetExtension(file.FileName) ?? "").ToLowerInvariant()))
                return AppraisalJson(400, new { message = "Select a PDF or CSV file up to 10 MB." });
            try {
                using (var buffer = new MemoryStream()) {
                    file.InputStream.CopyTo(buffer);
                    var bytes = buffer.ToArray();
                    var isPdf = string.Equals(Path.GetExtension(file.FileName), ".pdf", StringComparison.OrdinalIgnoreCase);
                    if (bytes.Length > 10 * 1024 * 1024 || (isPdf && (bytes.Length < 5 || System.Text.Encoding.ASCII.GetString(bytes, 0, 5) != "%PDF-")))
                        return AppraisalJson(400, new { message = "The selected file is not a valid PDF." });
                    using (var stream = new MemoryStream(bytes)) return AppraisalJson(200, new { vehicles = isPdf ? AppraisalPdfParser.Parse(stream) : AppraisalCsvParser.Parse(stream) });
                }
            } catch (InvalidOperationException ex) { return AppraisalJson(400, new { message = ex.Message }); }
            catch (Exception) { return AppraisalJson(400, new { message = "This file could not be read. Use an unlocked text-based PDF or a CSV with VIN and odometer mileage columns." }); }
        }
        [HttpPost, AsyncTimeout(90000)]
        public async Task<ActionResult> AppraiseBatchVehicle(string vin, int? miles, string zip, int? radius) {
            if (!CanUseAppraisal()) return AppraisalJson(403, new { message = "Sign in with a role that has Appraisal access." });
            if (!System.Text.RegularExpressions.Regex.IsMatch(vin ?? "", "^[A-HJ-NPR-Z0-9]{17}$") || !miles.HasValue || miles < 0 || miles > 2000000
                || !System.Text.RegularExpressions.Regex.IsMatch(zip ?? "", "^[0-9]{5}$") || !radius.HasValue || radius < 1 || radius > 500)
                return AppraisalJson(400, new { message = "Verify the VIN, odometer miles, ZIP and radius." });
            var estimatePath = BuildBatchAppraisalEstimatePath(vin, miles.Value, zip, radius.Value);
            var priceResponse = await SendAppraisalAsync(estimatePath, null, HttpMethod.Get);
            if (Response.StatusCode != 200) return priceResponse;
            var priceContent = priceResponse as ContentResult;
            if (priceContent == null) return AppraisalJson(502, new { message = "MarketCheck returned an unreadable estimate." });
            var price = (decimal?)JObject.Parse(priceContent.Content)["marketcheck_price"];
            if (!price.HasValue || price <= 0) return AppraisalJson(502, new { message = "MarketCheck returned no usable estimate." });
            var bidResponse = await SendAppraisalAsync("/auction-budget", new { expectedSellingPriceUsd = price.Value });
            var bidContent = bidResponse as ContentResult;
            var bid = bidContent == null ? null : JObject.Parse(bidContent.Content);
            if (Response.StatusCode != 200) return AppraisalJson(Response.StatusCode, new {
                marketCheckRetailUsd = price,
                message = "Estimate available. " + ((string)bid?["message"] ?? "The auction budget could not be calculated. Retry this vehicle.")
            });
            if (bid == null || bid["maximumHammerBidUsd"] == null) return AppraisalJson(502, new { marketCheckRetailUsd = price, message = "The auction service returned no budget result. Retry this vehicle." });
            return AppraisalJson(200, new { marketCheckRetailUsd = price, maximumHammerBidUsd = (decimal?)bid?["maximumHammerBidUsd"], message = bid?["maximumHammerBidUsd"]?.Type == JTokenType.Null ? "No feasible bid at the configured profit target." : "Complete" });
        }

        private static string BuildBatchAppraisalEstimatePath(string vin, int miles, string zip, int radius) {
            var searchMiles = AppraisalSearchMileage.Bucket(miles).Value;
            return FormattableString.Invariant($"/{vin}/json?miles={searchMiles}&zip={zip}&radius={radius}");
        }
    }
}
