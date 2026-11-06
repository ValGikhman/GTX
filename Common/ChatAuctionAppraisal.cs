using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Configuration;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using GTX.Models;

namespace GTX.Common
{
    internal sealed class ChatAuctionAppraisal
    {
        public const string ToolName = "appraise_vehicle";
        public const string MarketPriceToolName = "get_market_price";
        public const string MarketInventoryToolName = "count_market_inventory";
        public const string MarketListingsToolName = "list_market_dealers";
        private static readonly CultureInfo Dollars = CultureInfo.GetCultureInfo("en-US");
        private static readonly Regex AppraiseCommand = new Regex(
            @"\A(?:appraise|apprice)\s+(?:vin\s+)?`?(?<vin>[A-HJ-NPR-Z0-9]{17})`?(?:\s+with\s+(?<miles>[0-9]+|[0-9]{1,3}(?:,[0-9]{3})+)\s+miles)?(?<auction>\s+for\s+auction)?[.!?]?\z",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static readonly Regex ExplicitMarketMileage = new Regex(
            @"\b(?:with|around|about|at|roughly|approximately)\s+(?<miles>[0-9]{1,3}(?:,[0-9]{3})+|[0-9]+(?:\.[0-9]+)?)\s*(?<thousands>k)?\s*(?:odometer\s+)?(?:miles?|mi)\b(?!\s*(?:radius|away|from|of)\b)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static readonly HttpClient Http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false })
        {
            Timeout = TimeSpan.FromSeconds(35)
        };
        private readonly Uri endpoint;
        private readonly string key;

        private ChatAuctionAppraisal(Uri endpoint, string key)
        {
            this.endpoint = endpoint;
            this.key = key;
        }

        public static ChatAuctionAppraisal ForRole(CommonUnit.Roles role)
        {
            if (role != CommonUnit.Roles.Owner && role != CommonUnit.Roles.Manager) return null;

            var url = ConfigurationManager.AppSettings["AutoDealer:AppraisalBaseUrl"];
            var key = ConfigurationManager.AppSettings["AutoDealer:ApiKey"]?.Trim();
            if (string.IsNullOrEmpty(key) || !Uri.TryCreate(url, UriKind.Absolute, out var baseUrl)
                || baseUrl.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(baseUrl.UserInfo)
                || !string.IsNullOrEmpty(baseUrl.Query) || !string.IsNullOrEmpty(baseUrl.Fragment)) return null;

            return new ChatAuctionAppraisal(new Uri(baseUrl, "/api/service/price-research/"), key);
        }

        public static JObject Tool(string name)
        {
            var auction = name == ToolName;
            var inventory = name == MarketInventoryToolName;
            var marketList = name == MarketListingsToolName;
            var parameters = JObject.Parse(@"{
                    'type':'object',
                    'properties':{
                        'vin':{'type':'string'},
                        'miles':{'type':'integer','minimum':0,'maximum':2000000},
                        'zip':{'type':'string'}
                    },
                    'required':['vin'],
                    'additionalProperties':false
                }");
            var properties = (JObject)parameters["properties"];
            properties["radius"] = new JObject { ["type"] = "integer", ["minimum"] = 1, ["maximum"] = 500 };
            if (inventory)
                properties["miles"]["description"] = "The target odometer mileage of the comparable listings, such as 100000 for 100K miles.";
            if (auction)
            {
                properties["resaleUsd"] = new JObject { ["type"] = "number", ["description"] = "Optional resale assumption in US dollars." };
                properties["hammerBidUsd"] = new JObject { ["type"] = "number", ["description"] = "Optional current hammer bid in US dollars." };
            }
            if (marketList)
            {
                parameters.Remove("required");
                properties["year"] = new JObject { ["type"] = "integer", ["minimum"] = 1886 };
                properties["make"] = new JObject { ["type"] = "string" };
                properties["model"] = new JObject { ["type"] = "string" };
                properties["trim"] = new JObject { ["type"] = "string" };
            }
            return new JObject
            {
                ["type"] = "function",
                ["name"] = name,
                ["strict"] = false,
                ["description"] = marketList
                    ? "List current and recent MarketCheck dealers for a VIN or a vehicle year, make, model, and optional trim. Pass only the mileage, ZIP, and radius that the user supplied."
                    : inventory
                    ? "Decode a VIN and count matching active used-car listings. Leave mileage, ZIP, and radius out when the user omits them."
                    : auction
                    ? "Run an internal appraisal when the user explicitly asks for auction or bid analysis. Pass mileage, ZIP, and radius when supplied."
                    : "Return the MarketCheck suggested retail price for a VIN. Use for plain appraisal and market price requests. Pass mileage, ZIP, and radius when supplied. Radius is carried into the Appraisal page.",
                ["parameters"] = parameters
            };
        }

        public static bool TryParseCommand(string message, out string arguments, out bool auction)
        {
            var match = AppraiseCommand.Match(message);
            auction = match.Groups["auction"].Success;
            arguments = null;
            if (!match.Success) return false;
            var input = new JObject { ["vin"] = match.Groups["vin"].Value };
            if (match.Groups["miles"].Success)
            {
                if (!int.TryParse(match.Groups["miles"].Value, NumberStyles.AllowThousands, Dollars, out var miles)) return false;
                input["miles"] = miles;
            }
            arguments = input.ToString(Formatting.None);
            return true;
        }

        public static string ApplyMarketMileage(string arguments, string message)
        {
            var matches = ExplicitMarketMileage.Matches(message);
            if (matches.Count != 1) return arguments;

            var match = matches[0];
            if (!decimal.TryParse(match.Groups["miles"].Value, NumberStyles.AllowThousands | NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture, out var mileage) || mileage > 2000000m) return arguments;
            if (match.Groups["thousands"].Success) mileage *= 1000m;
            if (mileage > 2000000m || mileage != decimal.Truncate(mileage)) return arguments;

            var input = JObject.Parse(arguments ?? "{}");
            input["miles"] = (int)mileage;
            return input.ToString(Formatting.None);
        }

        public async Task<ChatMarketReply> ReplyAsync(string arguments, string toolName)
        {
            var auction = toolName == ToolName;
            var inventory = toolName == MarketInventoryToolName;
            var marketList = toolName == MarketListingsToolName;
            try
            {
                var input = JObject.Parse(arguments ?? "{}");
                var path = marketList ? "market-listings" : inventory ? "market-inventory" : auction ? "auction-appraisal" : "market-price";
                var url = new Uri(endpoint, path);
                using (var request = new HttpRequestMessage(HttpMethod.Post, url))
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
                    request.Content = new StringContent(input.ToString(Formatting.None), Encoding.UTF8, "application/json");
                    using (var response = await Http.SendAsync(request))
                    {
                        if (response.StatusCode == HttpStatusCode.BadRequest)
                            return new ChatMarketReply { Note = marketList ? "Enter a VIN or the vehicle year, make, and model, plus any ZIP, mileage, or radius you want to use."
                                : inventory ? "Enter a 17-character VIN, target mileage, and a valid ZIP and radius."
                                : "Send the 17-character VIN and actual mileage, and we can get the appraisal under way." };
                        if ((int)response.StatusCode == 429)
                            return new ChatMarketReply { Note = "The appraisal quota has been reached. Try again when more requests are available." };
                        if (!response.IsSuccessStatusCode)
                            return UnavailableReply();
                        var result = JObject.Parse(await response.Content.ReadAsStringAsync());
                        if (marketList) return FormatMarketListingsReply(result);
                        if (inventory) return FormatInventoryReply(result);
                        var reply = auction ? FormatReply(result) : FormatPriceReply(result);
                        reply.AppraisalParameters = new
                        {
                            vin = (string)result["vin"] ?? (string)input["vin"],
                            miles = (int?)result["miles"] ?? (int?)input["miles"],
                            zip = (string)result["zip"] ?? (string)input["zip"],
                            radius = (int?)result["radius"] ?? (int?)input["radius"],
                            resaleUsd = ((decimal?)input["resaleUsd"])?.ToString("0.##", CultureInfo.InvariantCulture),
                            hammerBidUsd = ((decimal?)input["hammerBidUsd"])?.ToString("0.##", CultureInfo.InvariantCulture)
                        };
                        return reply;
                    }
                }
            }
            catch (Exception ex) when (ex is JsonException || ex is HttpRequestException || ex is OperationCanceledException
                || ex is FormatException || ex is InvalidCastException || ex is OverflowException)
            {
                return UnavailableReply();
            }
        }

        private static ChatMarketReply UnavailableReply() => new ChatMarketReply
        {
            Note = "I couldn’t get an appraisal this time. Try again or check the appraisal connection."
        };

        private static ChatMarketReply FormatPriceReply(JObject result)
        {
            var price = (decimal?)result["marketCheckRetailUsd"];
            if (price == null || price <= 0) throw new JsonSerializationException();
            return new ChatMarketReply
            {
                Kicker = "MarketCheck",
                Title = "Market price",
                Metrics = new[] { Metric("Suggested retail price", Money(price.Value), primary: true) }
            };
        }

        private static ChatMarketReply FormatInventoryReply(JObject result)
        {
            var count = (long?)result["listingCount"];
            if (!count.HasValue || count < 0) throw new JsonSerializationException();
            var minimum = ((int)result["minimumMiles"]).ToString("N0", Dollars);
            var maximum = ((int)result["maximumMiles"]).ToString("N0", Dollars);
            return new ChatMarketReply
            {
                Kicker = "MarketCheck inventory",
                Title = (string)result["vehicle"],
                Context = minimum + "–" + maximum + " odometer miles · Within " + Number((int)result["radius"])
                    + " mi of ZIP " + (string)result["zip"],
                Metrics = new[] { Metric(count == 1 ? "Active used listing" : "Active used listings", Number(count.Value), primary: true) },
                Sections = count == 0 ? Array.Empty<ChatMarketSection>() : new[]
                {
                    FormatDealerList(result, count.Value, new ChatMarketSection { Title = "Matching dealers" })
                }
            };
        }

        private static ChatMarketReply FormatMarketListingsReply(JObject result)
        {
            return new ChatMarketReply
            {
                Kicker = "MarketCheck dealer listings",
                Title = (string)result["vehicle"],
                Context = "ZIP " + (string)result["zip"] + " · " + Number((int)result["minimumMiles"])
                    + "–" + Number((int)result["maximumMiles"]) + " odometer miles",
                Sections = new[]
                {
                    FormatMarketPeriod("Current", result["current"] as JObject, (int)result["radius"]),
                    FormatMarketPeriod("Recent (past 90 days)", result["recent"] as JObject, (int)result["recentRadius"])
                }
            };
        }

        private static ChatMarketSection FormatMarketPeriod(string title, JObject period, int radius)
        {
            var section = new ChatMarketSection { Title = title, Context = "Within " + Number(radius) + " miles" };
            if (period == null)
            {
                section.Note = "MarketCheck could not complete this search.";
                return section;
            }
            var count = (long)period["listingCount"];
            section.Context += " · " + Number(count) + (count == 1 ? " listing" : " listings");
            return FormatDealerList(period, count, section);
        }

        private static ChatMarketSection FormatDealerList(JObject result, long count, ChatMarketSection section)
        {
            if (count == 0) return section;

            var dealers = (result["dealers"] as JArray ?? new JArray()).OfType<JObject>().ToArray();
            if (dealers.Length == 0)
            {
                section.Note = "MarketCheck did not provide dealer names for these listings.";
                return section;
            }

            section.Items = dealers.Select(dealer =>
            {
                var listingCount = (int)dealer["listingCount"];
                var label = (string)dealer["label"] + (listingCount > 1 ? " · " + Number(listingCount) + " listings" : "");
                var listings = (dealer["listings"] as JArray)?.OfType<JObject>().Select(FormatListingPrice).ToArray();
                return label + (listings?.Length > 0 ? " · " + string.Join(" / ", listings) : " · Asking prices unavailable");
            }).ToArray();
            var namedListings = dealers.Sum(dealer => (int)dealer["listingCount"]);
            if (namedListings < count)
                section.Note = "Dealer names shown for " + Number(namedListings) + " of " + Number(count) + " listings.";
            return section;
        }

        private static string FormatListingPrice(JObject listing)
        {
            var price = (decimal?)listing["askingPriceUsd"];
            var miles = (decimal?)listing["miles"];
            var asking = price > 0 ? Money(price.Value) : "Price not listed";
            return asking + (miles >= 0 ? " at " + miles.Value.ToString("N0", Dollars) + " mi" : "");
        }

        private static ChatMarketReply FormatReply(JObject appraisal)
        {
            var bid = appraisal["bid"] as JObject;
            if (bid == null || appraisal["marketCheckRetailUsd"] == null) throw new JsonSerializationException();
            var comps = appraisal["comparables"] as JArray;
            var market = (decimal)appraisal["marketCheckRetailUsd"];
            var resale = (decimal)bid["ExpectedSellingPriceUsd"];
            var mileageLabel = (bool?)appraisal["usedDefaultMileage"] == true ? " assumed miles" : " miles";
            var maximum = (decimal?)bid["MaximumHammerBidUsd"];
            var prices = comps?.OfType<JObject>().Take(3).Select(FormatListingPrice);
            var evidence = comps == null ? "Nearby asking prices were unavailable."
                : comps.Count == 0 ? "No usable nearby asking prices."
                : Number(comps.Count) + (comps.Count == 1 ? " nearby asking price" : " nearby asking prices");
            if (comps?.Count > 3) evidence += " · Showing 3";
            var currentProfit = (decimal?)bid["CurrentProfitUsd"];
            var profit = currentProfit ?? (maximum.HasValue ? (decimal?)bid["ProfitAtMaximumUsd"] : null);
            var metrics = new[]
            {
                Metric("Maximum hammer bid", maximum.HasValue ? Money(maximum.Value) : "No feasible bid", primary: true),
                Metric("MarketCheck retail estimate", Money(market)),
                Metric("Expected resale", Money(resale)),
                Metric("Target profit", Money((decimal)bid["MinimumProfitUsd"])),
                profit.HasValue ? Metric(currentProfit.HasValue ? "Profit at your bid" : "Profit at maximum bid", Money(profit.Value)) : null
            };
            return new ChatMarketReply
            {
                Kicker = "Auction appraisal",
                Title = (string)appraisal["vehicle"] ?? (string)appraisal["vin"],
                Context = Number((int)appraisal["miles"]) + mileageLabel + " · " + Number((int)appraisal["radius"])
                    + " mi of ZIP " + (string)appraisal["zip"],
                Metrics = metrics.Where(metric => metric != null).ToArray(),
                Sections = new[] { new ChatMarketSection { Title = "Market evidence", Context = evidence, Items = prices?.ToArray() ?? Array.Empty<string>() } },
                Note = Money((decimal)bid["AuctionFeeUsd"]) + " auction fee · " + Money((decimal)bid["TransportUsd"])
                    + " transport. Profit is before other expenses."
                    + (maximum.HasValue ? "" : " The resale assumption leaves too little room for the target profit.")
            };
        }

        private static ChatMarketMetric Metric(string label, string value, bool primary = false) =>
            new ChatMarketMetric { Label = label, Value = value, IsPrimary = primary };

        private static string Number(long amount) => amount.ToString("N0", Dollars);

        private static string Money(decimal amount) => amount.ToString(amount == decimal.Truncate(amount) ? "C0" : "C2", Dollars);
    }
}
