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
using System.Threading;
using System.Threading.Tasks;

namespace GTX.Controllers
{
    public sealed partial class ChatController
    {
        private static readonly Regex RetailMarketMention = new Regex(@"\b(?:car\s*max|carvana|cars\.com|(?:search|check)\s+market\s*check\s+for|compare\s+market\s*check)\b", RegexOptions.IgnoreCase);
        private static readonly Regex AuctionListingRequest = new Regex(@"\b(?:auction|hammer bid)\b", RegexOptions.IgnoreCase);

        private async Task<ChatMarketReply> GetRetailMarketReplyAsync(string message, Guid? progressId)
        {
            var carMax = Regex.IsMatch(message, @"\bcar\s*max\b", RegexOptions.IgnoreCase);
            var carvana = Regex.IsMatch(message, @"\bcarvana\b", RegexOptions.IgnoreCase);
            var carsCom = Regex.IsMatch(message, @"\bcars\.com\b", RegexOptions.IgnoreCase);
            var carGurus = CarGurusMention.IsMatch(message);
            var sources = new[] { carMax ? "CarMax" : null, carvana ? "Carvana" : null, carsCom ? "Cars.com" : null }
                .Where(source => source != null).DefaultIfEmpty("MarketCheck").ToArray();
            ChatProgressController.StartMarket(progressId);
            var marketTask = SearchRetailMarketAsync(message, sources);
            var gurusTask = carGurus && CarGurusAuctionResearch.Enabled
                ? GetCarGurusReplyAsync(message, progressId)
                : Task.FromResult(new ChatMarketReply { Note = carGurus ? "CarGurus search is disabled." : null });
            await Task.WhenAll(marketTask, gurusTask);

            var marketResults = await marketTask;
            var groups = sources.Select((source, index) =>
                CreateListingGroup(source, marketResults.Length == sources.Length ? marketResults[index] : marketResults[0]))
                .ToList();
            if (carGurus) groups.Add(CreateListingGroup("CarGurus", await gurusTask));

            return new ChatMarketReply
            {
                Kicker = "Retail inventory",
                Title = "Listings from " + string.Join(", ", groups.Select(group => group.Name)),
                ListingGroups = groups.ToArray()
            };
        }

        private static ChatMarketListingGroup CreateListingGroup(string name, ChatMarketReply reply) =>
            new ChatMarketListingGroup
            {
                Name = name,
                Context = reply.Context,
                Listings = reply.Listings,
                Note = reply.Note,
                ViewUrl = reply.ViewUrl
            };

        private async Task<ChatMarketReply[]> SearchRetailMarketAsync(string message, string[] sources)
        {
            try
            {
                var client = new MarketCheckMcpClient();
                var vin = CarGurusVin.Matches(message).Cast<Match>().Select(match => match.Value.ToUpperInvariant()).Distinct().ToArray();
                if (vin.Length > 1)
                    return new[] { new ChatMarketReply { Note = "Please send one VIN at a time for MarketCheck." } };

                var criteria = message;
                if (vin.Length == 1)
                {
                    var decoded = await DecodeCarGurusVehicleAsync(vin[0]);
                    if (decoded == null)
                        return new[] { new ChatMarketReply { Note = "I couldn't decode that VIN. Try the year, make, and model." } };
                    criteria = CarGurusVin.Replace(message, decoded.Item2);
                }

                var args = await ReadRetailCriteriaAsync(criteria);
                if (args == null)
                    return new[] { new ChatMarketReply { Note = "Enter a vehicle make and model or a VIN to search retail inventory." } };

                var zip = CarGurusZip.Match(message).Groups["zip"].Value;
                var radius = CarGurusRadius.Match(message).Groups["radius"].Value;
                var explicitLocation = !string.IsNullOrEmpty(zip) || !string.IsNullOrEmpty(radius);
                zip = string.IsNullOrEmpty(zip) ? MajordomeController.AppraisalDefaultZip : zip;
                radius = string.IsNullOrEmpty(radius) ? MajordomeController.AppraisalDefaultRadius : radius;
                args["car_type"] = "used";
                args["rows"] = 10;
                args["include_build_object"] = true;
                args["include_dealer_object"] = true;

                // Calls share an MCP session and run sequentially.
                using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(25)))
                {
                    try
                    {
                        await client.InitializeAsync(timeout.Token);
                        var replies = new ChatMarketReply[sources.Length];
                        for (var i = 0; i < sources.Length; i++)
                        {
                            var source = sources[i];
                            var sourceArgs = (JObject)args.DeepClone();
                            if (source != "MarketCheck")
                                sourceArgs["source"] = source == "Cars.com" ? "cars.com" : source.ToLowerInvariant() + ".com";
                            if (source == "MarketCheck" || explicitLocation)
                            {
                                sourceArgs["zip"] = zip;
                                sourceArgs["radius"] = CarGurusNumber(radius);
                                sourceArgs["sort_by"] = "dist";
                            }
                            replies[i] = await SearchRetailSourceAsync(client, source, sourceArgs, vin.SingleOrDefault(), timeout.Token);
                        }
                        return replies;
                    }
                    finally
                    {
                        await client.CloseAsync();
                    }
                }
            }
            catch (ConfigurationErrorsException)
            {
                return new[] { new ChatMarketReply { Note = "Check the MarketCheck MCP and OpenAI settings for retail search." } };
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is OperationCanceledException
                || ex is JsonException || ex is OpenAiRequestException)
            {
                return new[] { new ChatMarketReply { Note = "MarketCheck retail search is temporarily unavailable." } };
            }
        }

        private async Task<JObject> ReadRetailCriteriaAsync(string message)
        {
            var key = ConfigurationManager.AppSettings["OpenAI:ApiKey"];
            if (string.IsNullOrWhiteSpace(key)) throw new HttpRequestException("Retail search needs OpenAI.");
            var properties = new JObject();
            foreach (var name in new[] { "make", "model", "trim", "drivetrain" })
                properties[name] = new JObject { ["type"] = "string" };
            foreach (var name in new[] { "year", "max_miles", "min_price", "max_price" })
                properties[name] = new JObject { ["type"] = "integer" };

            var response = await PostOpenAiAsync(key, new JObject
            {
                ["model"] = DefaultChatModel,
                ["instructions"] = "Extract only explicitly requested vehicle search filters. Omit unknown filters. "
                    + "Convert k to thousands and exclusive limits to inclusive integers. Do not guess a make or model. "
                    + "Odometer miles are separate from geographic distance. Omit mileage unless a maximum is stated.",
                ["input"] = message,
                ["tools"] = new JArray(new JObject
                {
                    ["type"] = "function",
                    ["name"] = "retail_filters",
                    ["strict"] = false,
                    ["parameters"] = new JObject { ["type"] = "object", ["properties"] = properties, ["additionalProperties"] = false }
                }),
                ["tool_choice"] = "required",
                ["parallel_tool_calls"] = false,
                ["max_output_tokens"] = 300,
                ["store"] = false,
                ["safety_identifier"] = BuildSafetyIdentifier()
            });

            var call = (response["output"] as JArray)?.OfType<JObject>()
                .FirstOrDefault(item => (string)item["type"] == "function_call" && (string)item["name"] == "retail_filters");
            var extracted = JObject.Parse((string)call?["arguments"] ?? "{}");
            var args = new JObject();
            foreach (var name in new[] { "make", "model", "trim", "drivetrain" })
            {
                var value = extracted[name]?.Type == JTokenType.String ? ((string)extracted[name]).Trim() : null;
                if (!string.IsNullOrEmpty(value) && value.Length <= 80 && !value.Contains(",")) args[name] = value;
            }
            if (args["make"] == null || args["model"] == null) return null;
            if (ReadNumber(extracted["year"], 2100) is int year && year >= 1886) args["year"] = year;
            if (ReadNumber(extracted["max_miles"], 2000000) is int miles) args["miles_range"] = "0-" + miles;
            var min = ReadNumber(extracted["min_price"], 100000000);
            var max = ReadNumber(extracted["max_price"], 100000000);
            if (min.HasValue || max.HasValue) args["price_range"] = $"{min ?? 0}-{max ?? 100000000}";
            return args;
        }

        private static int? ReadNumber(JToken value, int maximum) =>
            value?.Type == JTokenType.Integer && int.TryParse(value.ToString(), out var number)
                && number >= 0 && number <= maximum ? (int?)number : null;

        private static async Task<ChatMarketReply> SearchRetailSourceAsync(MarketCheckMcpClient client, string source,
            JObject args, string searchedVin, CancellationToken token)
        {
            try
            {
                var data = await client.CallAsync("search_active_cars", args, token);
                if (!(data["listings"] is JArray listings))
                    throw new HttpRequestException("MarketCheck returned no listing array.");

                var rows = listings.OfType<JObject>().Select(row =>
                {
                    var build = row["build"] as JObject;
                    var dealer = row["dealer"] as JObject;
                    var vin = (string)row["vin"];
                    var url = (string)row["vdp_url"];
                    var price = (decimal?)row["price"];
                    var miles = (int?)row["miles"];
                    var distance = (decimal?)row["dist"];
                    return new ChatMarketListing
                    {
                        Vin = vin,
                        IsSearchedVin = searchedVin != null && string.Equals(vin, searchedVin, StringComparison.OrdinalIgnoreCase),
                        Vehicle = (string)row["heading"] ?? Join((string)build?["year"], (string)build?["make"], (string)build?["model"], (string)build?["trim"]),
                        Location = string.Join(", ", new[] { (string)dealer?["name"], Join((string)dealer?["city"], (string)dealer?["state"]) }
                            .Where(value => !string.IsNullOrWhiteSpace(value))),
                        Ask = price > 0 ? price.Value.ToString("C0", CultureInfo.GetCultureInfo("en-US")) : null,
                        Odometer = miles >= 0 ? miles.Value.ToString("N0", CultureInfo.GetCultureInfo("en-US")) : null,
                        Distance = distance >= 0 ? distance.Value.ToString("0.#", CultureInfo.InvariantCulture) : null,
                        Url = Uri.TryCreate(url, UriKind.Absolute, out var link) && link.Scheme == Uri.UriSchemeHttps
                            && string.IsNullOrEmpty(link.UserInfo) ? link.AbsoluteUri : null
                    };
                }).Where(row => !string.IsNullOrWhiteSpace(row.Vehicle)).ToArray();

                return new ChatMarketReply
                {
                    Context = args["zip"] == null
                        ? null
                        : $"ZIP {args["zip"]} · {args["radius"]}-mile search radius",
                    Listings = rows,
                    Note = rows.Length == 0 ? source + " found no matching listings." : null
                };
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is OperationCanceledException
                || ex is JsonException || ex is FormatException || ex is InvalidCastException || ex is OverflowException)
            {
                return new ChatMarketReply { Note = source + " listings were unavailable from MarketCheck." };
            }
        }
    }
}
