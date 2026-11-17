using GTX.Common;
using GTX.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Configuration;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace GTX.Controllers
{
    public sealed partial class ChatController
    {
        private static readonly Regex MarketAppraisalMention = new Regex(
            @"\b(?:apprais(?:e|al)|valuation|comparables?|comps?)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static readonly Regex RequestedComparableCount = new Regex(
            @"\b(?<count>[0-9]{1,3}|one|two|three|four|five|six|seven|eight|nine|ten)\s+(?:comparables?|comps?)\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static readonly Regex AppraisalRadius = new Regex(
            @"\b(?:within\s+(?<radius>-?[0-9]+(?:\.[0-9]+)?)\s*-?\s*(?:miles?|mi)\b|(?<radius>-?[0-9]+(?:\.[0-9]+)?)\s*-?\s*(?:miles?|mi)\s+radius\b|radius\s*(?:of\s+|:\s*)?(?<radius>-?[0-9]+(?:\.[0-9]+)?)\b)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        private async Task<ChatMarketReply> GetMarketAppraisalReplyAsync(string message, Guid? progressId, ChatAuctionAppraisal client)
        {
            if (client == null) return MarketAppraisalError(
                "Oy vey, the appraisal connection needs care,\nAsk an administrator to check Services there.\nThe research URL and key must be right,\nThen we can bring MarketCheck into sight.");
            try
            {
                var key = ServiceConfiguration.Get("OpenAI:ApiKey");
                if (string.IsNullOrWhiteSpace(key)) return MarketAppraisalError(
                    "Oy vey, the assistant's connection needs care,\nAsk an administrator to check Services there.\nAn OpenAI key must be ready to go,\nThen the appraisal request can flow.");
                // Extract a small request only. Provider responses and the dealership's
                // inventory are never sent back to the model for this workflow.
                var response = await PostOpenAiAsync(key, new JObject
                {
                    ["model"] = DefaultChatModel,
                    ["instructions"] = "Extract only the vehicle and appraisal inputs explicitly supplied in this message. "
                        + "Use a supplied VIN, or year, make, model and optional trim. Never invent a VIN. "
                        + "Convert 42k miles to miles=42000. Subject odometer miles are not a search radius. "
                        + "Omit ZIP, radius and mileage when not supplied. comparableCount defaults to 10, maximum 10. "
                        + "Do not infer title, Carfax, condition, certification or price adjustments.",
                    ["input"] = message,
                    ["tools"] = new JArray(ChatAuctionAppraisal.Tool(ChatAuctionAppraisal.MarketAppraisalToolName)),
                    ["tool_choice"] = "required", ["parallel_tool_calls"] = false,
                    ["max_output_tokens"] = 400, ["store"] = false,
                    ["safety_identifier"] = BuildSafetyIdentifier()
                });
                var call = (response["output"] as JArray)?.OfType<JObject>().FirstOrDefault(item =>
                    (string)item["type"] == "function_call" && (string)item["name"] == ChatAuctionAppraisal.MarketAppraisalToolName);
                var input = NormalizeMarketAppraisalArguments(JObject.Parse((string)call?["arguments"] ?? "{}"), message);
                if (input == null) return MarketAppraisalError(
                    "Oy vey, send a VIN or year, make, and model my way,\nAdd the odometer miles for the price today.\nA ZIP and search radius can guide the view,\nThen up to ten comparables can come back to you.");
                ChatProgressController.StartMarketAppraisal(progressId);
                return await GetMarketReplyAsync(client, input.ToString(Formatting.None), ChatAuctionAppraisal.MarketAppraisalToolName);
            }
            catch (Exception ex) when (ex is JsonException || ex is HttpRequestException || ex is OperationCanceledException
                || ex is ConfigurationErrorsException || ex is OpenAiRequestException || ex is FormatException || ex is OverflowException)
            {
                return MarketAppraisalError("Oy vey, the appraisal could not come through,\nPlease check the connection and try anew.\nNo confirmed price is ready to show,\nOy, retry the search before you go.");
            }
        }

        private static ChatMarketReply MarketAppraisalError(string note) => new ChatMarketReply { Note = note };

        internal static JObject NormalizeMarketAppraisalArguments(JObject extracted, string message)
        {
            var input = new JObject();
            var vins = CarGurusVin.Matches(message ?? string.Empty).Cast<Match>()
                .Select(match => match.Value.ToUpperInvariant()).Distinct().ToArray();
            if (vins.Length > 1) return null;
            if (vins.Length == 1) input["vin"] = vins[0];
            else
            {
                var year = ReadNumber(extracted["year"], DateTime.UtcNow.Year + 2);
                if (!(year >= 1886) || !Regex.IsMatch(message ?? string.Empty, @"\b" + year.Value + @"\b")) return null;
                input["year"] = year.Value;
                foreach (var name in new[] { "make", "model", "trim" })
                {
                    var value = extracted[name]?.Type == JTokenType.String ? ((string)extracted[name]).Trim() : null;
                    if (string.IsNullOrEmpty(value)) { if (name != "trim") return null; continue; }
                    if (value.Length > 80 || value.Contains(",") || value.Any(char.IsControl)
                        || !(" " + NormalizeCommandPhrase(message) + " ").Contains(" " + NormalizeCommandPhrase(value) + " ")) return null;
                    input[name] = value;
                }
            }
            // The model identifies vehicle words; numeric inputs come from the user's
            // literal text so a radius cannot become a guessed odometer or ZIP.
            var zips = CarGurusZip.Matches(message ?? string.Empty).Cast<Match>().Select(match => match.Groups["zip"].Value).Distinct().ToArray();
            if (zips.Length > 1) return null;
            if (zips.Length == 1) input["zip"] = zips[0];
            var radii = AppraisalRadius.Matches(message ?? string.Empty).Cast<Match>().ToArray();
            var radiusValues = radii.Select(match => match.Groups["radius"].Value).Distinct().ToArray();
            if (radiusValues.Length > 1) return null;
            if (radiusValues.Length == 1)
            {
                if (!int.TryParse(radiusValues[0], out var radius) || radius < 1 || radius > 500) return null;
                input["radius"] = radius;
            }
            var odometers = CarGurusOdometer.Matches(message ?? string.Empty).Cast<Match>()
                .Where(match => !radii.Any(radius => match.Index < radius.Index + radius.Length && radius.Index < match.Index + match.Length)).ToArray();
            var mileage = new System.Collections.Generic.List<int>();
            foreach (var odometer in odometers)
            {
                if (odometer.Groups["operator"].Success || odometer.Index > 0 && (message[odometer.Index - 1] == '-' || message[odometer.Index - 1] == '\u2212')) return null;
                if (!decimal.TryParse(odometer.Groups["miles"].Value, NumberStyles.AllowThousands | NumberStyles.AllowDecimalPoint,
                    CultureInfo.InvariantCulture, out var miles)) return null;
                if (odometer.Groups["thousands"].Success) miles *= 1000m;
                if (miles < 0 || miles > 2000000m || miles != decimal.Truncate(miles)) return null;
                mileage.Add((int)miles);
            }
            var subjectMiles = mileage.Distinct().ToArray();
            if (subjectMiles.Length > 1) return null;
            if (subjectMiles.Length == 1) input["miles"] = subjectMiles[0];
            var count = RequestedComparableCount.Match(message ?? string.Empty);
            if (count.Success)
            {
                var text = count.Groups["count"].Value;
                if (!int.TryParse(text, out var requested)) requested = Array.IndexOf(new[] { "one", "two", "three", "four", "five", "six", "seven", "eight", "nine", "ten" }, text.ToLowerInvariant()) + 1;
                if (requested < 1) return null;
                input["comparableCount"] = Math.Min(requested, 10);
            }
            return input;
        }
    }
}
