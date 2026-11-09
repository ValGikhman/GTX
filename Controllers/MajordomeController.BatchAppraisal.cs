using GTX.Common;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;

namespace GTX.Controllers {
    public partial class MajordomeController {
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
        [HttpPost, ValidateAntiForgeryToken, AsyncTimeout(90000)]
        public async Task<ActionResult> AppraiseBatchVehicle(string vin, int? miles, string zip, int? radius) {
            if (!CanUseAppraisal()) return new HttpStatusCodeResult(403);
            if (!System.Text.RegularExpressions.Regex.IsMatch(vin ?? "", "^[A-HJ-NPR-Z0-9]{17}$") || !miles.HasValue || miles < 0 || miles > 2000000
                || !System.Text.RegularExpressions.Regex.IsMatch(zip ?? "", "^[0-9]{5}$") || !radius.HasValue || radius < 1 || radius > 500)
                return AppraisalJson(400, new { message = "Verify the VIN, odometer miles, ZIP and radius." });
            var priceResponse = await SendAppraisalAsync("/market-price", new { vin, miles, zip, radius });
            if (Response.StatusCode != 200) return priceResponse;
            var priceContent = priceResponse as ContentResult;
            if (priceContent == null) return AppraisalJson(502, new { message = "MarketCheck returned an unreadable estimate." });
            var price = (decimal?)JObject.Parse(priceContent.Content)["marketCheckRetailUsd"];
            if (!price.HasValue || price <= 0) return AppraisalJson(502, new { message = "MarketCheck returned no usable estimate." });
            var bidResponse = await SendAppraisalAsync("/auction-budget", new { expectedSellingPriceUsd = price.Value });
            if (Response.StatusCode != 200) return AppraisalJson(Response.StatusCode, new { marketCheckRetailUsd = price, message = "Estimate available, but the auction budget could not be calculated. Retry this vehicle." });
            var bidContent = bidResponse as ContentResult;
            var bid = bidContent == null ? null : JObject.Parse(bidContent.Content);
            if (bid == null || bid["maximumHammerBidUsd"] == null) return AppraisalJson(502, new { marketCheckRetailUsd = price, message = "The auction service returned no budget result. Retry this vehicle." });
            return AppraisalJson(200, new { marketCheckRetailUsd = price, maximumHammerBidUsd = (decimal?)bid?["maximumHammerBidUsd"], message = bid?["maximumHammerBidUsd"]?.Type == JTokenType.Null ? "No feasible bid at the configured profit target." : "Complete" });
        }
    }
}
