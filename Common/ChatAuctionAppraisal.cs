using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
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
        public const string MarketAppraisalToolName = "appraise_market_vehicle";
        private static readonly CultureInfo Dollars = CultureInfo.GetCultureInfo("en-US");
        private static readonly Regex AppraiseCommand = new Regex(
            @"\A(?:appraise|apprice)\s+(?:vin\s+)?`?(?<vin>[A-HJ-NPR-Z0-9]{17})`?(?:\s+with\s+(?<miles>[0-9]{1,3}(?:[, \u00a0\u202f][0-9]{3})+|[0-9]+)\s+miles)?(?<auction>\s+for\s+auction)?[.!?]?\z",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static readonly Regex ExplicitMarketMileage = new Regex(
            @"\b(?:with|around|about|at|roughly|approximately)\s+(?<miles>[0-9]{1,3}(?:[, \u00a0\u202f][0-9]{3})+|[0-9]+(?:\.[0-9]+)?)\s*(?<thousands>k)?\s*(?:odometer\s+)?(?:miles?|mi)\b(?!\s*(?:radius|away|from|of)\b)",
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

        public static ChatAuctionAppraisal ForRole(SiteRoleModel role)
        {
            if (!SiteAccess.Can(role, MajordomeItem.Appraisal)) return null;

            var url = ServiceConfiguration.Get("AutoDealer:AppraisalBaseUrl");
            var key = ServiceConfiguration.Get("AutoDealer:ApiKey")?.Trim();
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
            var marketAppraisal = name == MarketAppraisalToolName;
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
            if (marketList || marketAppraisal)
            {
                parameters.Remove("required");
                properties["year"] = new JObject { ["type"] = "integer", ["minimum"] = 1886 };
                properties["make"] = new JObject { ["type"] = "string" };
                properties["model"] = new JObject { ["type"] = "string" };
                properties["trim"] = new JObject { ["type"] = "string" };
            }
            if (marketAppraisal)
                properties["comparableCount"] = new JObject { ["type"] = "integer", ["minimum"] = 1, ["maximum"] = 10,
                    ["description"] = "Number of comparable active listings requested, up to 10; default 10." };
            return new JObject
            {
                ["type"] = "function",
                ["name"] = name,
                ["strict"] = false,
                ["description"] = marketAppraisal
                    ? "Appraise a VIN or a vehicle year, make, model and optional trim using MarketCheck MCP, with up to 10 active comparables. Pass the subject odometer miles separately from the geographic radius. A named vehicle uses an identified reference VIN, not the user's actual vehicle."
                    : marketList
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
                if (!int.TryParse(Regex.Replace(match.Groups["miles"].Value, @"\s", ""), NumberStyles.AllowThousands, Dollars, out var miles)) return false;
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
            if (!decimal.TryParse(Regex.Replace(match.Groups["miles"].Value, @"\s", ""), NumberStyles.AllowThousands | NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture, out var mileage) || mileage > 2000000m) return arguments;
            if (match.Groups["thousands"].Success) mileage *= 1000m;
            if (mileage > 2000000m || mileage != decimal.Truncate(mileage)) return arguments;

            var input = JObject.Parse(arguments ?? "{}");
            input["miles"] = (int)mileage;
            return input.ToString(Formatting.None);
        }

        internal static void ApplyProfileDefaults(JObject input, SiteProfileModel profile)
        {
            if (string.IsNullOrWhiteSpace((string)input["zip"])) input["zip"] = profile.ZipCode;
            if (input["radius"] == null || input["radius"].Type == JTokenType.Null) input["radius"] = profile.ResearchRadiusMiles;
        }

        public async Task<JObject> SearchRetailListingsAsync(JObject input, System.Threading.CancellationToken token)
        {
            ApplyProfileDefaults(input, SiteProfileStore.Current);
            using (var request = new HttpRequestMessage(HttpMethod.Post, new Uri(endpoint, "retail-listings")))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
                request.Headers.Accept.ParseAdd("application/json");
                request.Headers.UserAgent.ParseAdd("GTXAutoGroup/1.0");
                request.Headers.ExpectContinue = false;
                request.Content = new StringContent(input.ToString(Formatting.None), Encoding.UTF8, "application/json");
                using (var response = await Http.SendAsync(request, token))
                {
                    response.EnsureSuccessStatusCode();
                    return JObject.Parse(await response.Content.ReadAsStringAsync());
                }
            }
        }

        public async Task<ChatMarketReply> ReplyAsync(string arguments, string toolName)
        {
            var auction = toolName == ToolName;
            var inventory = toolName == MarketInventoryToolName;
            var marketList = toolName == MarketListingsToolName;
            var marketAppraisal = toolName == MarketAppraisalToolName;
            try
            {
                var input = JObject.Parse(arguments ?? "{}");
                ApplyProfileDefaults(input, SiteProfileStore.Current);
                var path = marketAppraisal ? "market-appraisal" : marketList ? "market-listings" : inventory ? "market-inventory" : auction ? "auction-appraisal" : "market-price";
                var url = new Uri(endpoint, path);
                using (var request = new HttpRequestMessage(HttpMethod.Post, url))
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
                    request.Headers.UserAgent.ParseAdd("GTXAutoGroup/1.0");
                    request.Headers.ExpectContinue = false;
                    request.Content = new StringContent(input.ToString(Formatting.None), Encoding.UTF8, "application/json");
                    using (var response = await Http.SendAsync(request))
                    {
                        if (marketAppraisal && response.StatusCode == HttpStatusCode.NotFound)
                            return new ChatMarketReply { Note = "Oy vey, no matching reference VIN came through,\nCheck the year, make, model, and trim in view.\nA real vehicle VIN can help us get it right,\nThen we'll ask MarketCheck for a fresh insight." };
                        if (marketAppraisal && response.StatusCode == HttpStatusCode.ServiceUnavailable
                            && (await response.Content.ReadAsStringAsync()).IndexOf("MCP service", StringComparison.OrdinalIgnoreCase) >= 0)
                            return new ChatMarketReply { Note = "Oy vey, MarketCheck's local MCP service is out of view,\nStart the configured service, then try anew.\nNo confirmed appraisal is ready to show,\nOy, reconnect the service before prices can flow." };
                        if (response.StatusCode == HttpStatusCode.BadRequest)
                            return new ChatMarketReply { Note = marketAppraisal ? "Oy vey, send a VIN or year, make, and model my way,\nAdd the odometer miles for the price today.\nA ZIP and search radius can guide the view,\nThen up to ten comparables can come back to you."
                                : marketList ? "Enter a VIN or the vehicle year, make, and model, plus any ZIP, mileage, or radius you want to use."
                                : inventory ? "Enter a 17-character VIN, target mileage, and a valid ZIP and radius."
                                : "Send the 17-character VIN and actual mileage, and we can get the appraisal under way." };
                        if ((int)response.StatusCode == 429)
                        {
                            var body = await response.Content.ReadAsStringAsync();
                            var providerLimit = body.IndexOf("MarketCheck", StringComparison.OrdinalIgnoreCase) >= 0;
                            if (marketAppraisal) return new ChatMarketReply {
                                Note = "Oy vey, a request limit stopped this try,\nCheck " + (providerLimit ? "MarketCheck's" : "the appraisal account's") + " quota before requests can fly.\nWait until the account has room for more,\nThen ask again, and we'll knock on its door."
                            };
                            return new ChatMarketReply { Note = providerLimit
                                ? "MarketCheck has reached its API request limit. Check your MarketCheck account's monthly quota and rate limits, then retry when requests are available."
                                : "The appraisal quota has been reached. Try again when more requests are available." };
                        }
                        if (!response.IsSuccessStatusCode)
                            return UnavailableReply(marketAppraisal);
                        var result = JObject.Parse(await response.Content.ReadAsStringAsync());
                        if (marketAppraisal) return FormatMarketAppraisalReply(result);
                        if (marketList) return FormatMarketListingsReply(result);
                        if (inventory) return FormatInventoryReply(result);
                        var resale = (decimal?)input["resaleUsd"];
                        var reply = auction ? FormatReply(result) : FormatPriceReply(result);
                        reply.AppraisalParameters = new
                        {
                            vin = (string)result["vin"] ?? (string)input["vin"],
                            miles = (int?)result["miles"] ?? (int?)input["miles"],
                            zip = (string)result["zip"] ?? (string)input["zip"],
                            radius = (int?)result["radius"] ?? (int?)input["radius"],
                            resaleUsd = resale?.ToString("0.##", CultureInfo.InvariantCulture),
                            hammerBidUsd = ((decimal?)input["hammerBidUsd"])?.ToString("0.##", CultureInfo.InvariantCulture)
                        };
                        return reply;
                    }
                }
            }
            catch (Exception ex) when (ex is JsonException || ex is HttpRequestException || ex is OperationCanceledException
                || ex is FormatException || ex is InvalidCastException || ex is OverflowException)
            {
                return UnavailableReply(marketAppraisal);
            }
        }

        private static ChatMarketReply UnavailableReply(bool marketAppraisal = false) => new ChatMarketReply
        {
            Note = marketAppraisal
                ? "Oy vey, the appraisal could not come through,\nPlease check the connection and try anew.\nNo confirmed price is ready to show,\nOy, retry the search before you go."
                : "I couldn’t get an appraisal this time. Try again or check the appraisal connection."
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

        internal static ChatMarketReply FormatMarketAppraisalReply(JObject result)
        {
            var estimate = (decimal?)result["marketCheckRetailUsd"];
            if (!(estimate > 0)) throw new JsonSerializationException();
            var statistics = result["statistics"] as JObject ?? new JObject();
            var metrics = new System.Collections.Generic.List<ChatMarketMetric> {
                Metric("MarketCheck retail estimate", Money(estimate.Value), primary: true)
            };
            foreach (var entry in new[] {
                new[] { "msrpUsd", "Original MSRP" }, new[] { "medianPriceUsd", "Sample median ask" },
                new[] { "meanPriceUsd", "Sample mean ask" }
            }) {
                var amount = (decimal?)(entry[0] == "msrpUsd" ? result[entry[0]] : statistics[entry[0]]);
                if (amount > 0) metrics.Add(Metric(entry[1], Money(amount.Value)));
            }
            var lower = (decimal?)statistics["p25PriceUsd"];
            var upper = (decimal?)statistics["p75PriceUsd"];
            if (lower > 0 && upper >= lower) metrics.Add(Metric("Sample middle 50%", Money(lower.Value) + "–" + Money(upper.Value)));

            var listings = (result["comparables"] as JArray ?? new JArray()).OfType<JObject>().Take(10).Select(row => {
                var asking = (decimal?)row["askingPriceUsd"];
                var miles = (int?)row["miles"];
                var days = (decimal?)row["daysListed"];
                var distance = (decimal?)row["distanceMiles"];
                var rawUrl = (string)row["url"];
                return new ChatMarketListing {
                    Vin = (string)row["vin"], Vehicle = (string)row["vehicle"],
                    Ask = asking > 0 ? Money(asking.Value) : null,
                    Odometer = miles >= 0 ? Number(miles.Value) : null,
                    DaysListed = days >= 0 ? days.Value.ToString("0.#", Dollars) : null,
                    Distance = distance >= 0 ? distance.Value.ToString("0.#", CultureInfo.InvariantCulture) : null,
                    Location = string.Join(", ", new[] { (string)row["dealerName"], (string)row["city"], (string)row["state"] }
                        .Where(value => !string.IsNullOrWhiteSpace(value))),
                    Url = Uri.TryCreate(rawUrl, UriKind.Absolute, out var url) && url.Scheme == Uri.UriSchemeHttps
                        && string.IsNullOrEmpty(url.UserInfo) ? url.AbsoluteUri : null
                };
            }).ToArray();
            var sample = (int?)statistics["sampledListingCount"] ?? 0;
            var priced = (int?)statistics["pricedSampleCount"] ?? 0;
            var matched = (long?)result["listingCount"];
            var details = new System.Collections.Generic.List<string> {
                Number(sample) + " sampled listings · " + Number(priced) + " with asking prices. Statistics describe this fetched sample."
            };
            if (matched >= 0) details.Add(Number(matched.Value) + " matching active listings reported by MarketCheck within this search scope.");
            var minimum = (decimal?)statistics["minPriceUsd"];
            var maximum = (decimal?)statistics["maxPriceUsd"];
            if (minimum > 0 && maximum >= minimum) details.Add("Sample asking-price range: " + Money(minimum.Value) + "–" + Money(maximum.Value) + ".");
            var meanMiles = (decimal?)statistics["meanMiles"];
            if (meanMiles >= 0) details.Add("Sample average mileage: " + meanMiles.Value.ToString("N0", Dollars) + " miles.");
            if ((bool?)result["usedReferenceVin"] == true)
                details.Add("Reference VIN: " + (string)result["vin"] + ". Indicative estimate for the named vehicle; this is not your vehicle's VIN.");
            else details.Add("Subject VIN: " + (string)result["vin"] + ". The subject is excluded from its comparable listings.");
            var medianDays = (decimal?)statistics["medianDaysListed"];
            if (medianDays >= 0) details.Add("Sample median days listed: " + medianDays.Value.ToString("0.#", Dollars) + ".");
            if (listings.Length == 0) details.Add("No matching comparable listings were returned for this scope.");

            return new ChatMarketReply {
                Kicker = "MarketCheck appraisal", Title = (string)result["vehicle"] ?? (string)result["vin"],
                Context = Number((int)result["miles"]) + ((bool?)result["usedDefaultMileage"] == true ? " assumed miles" : " miles")
                    + " · Within " + Number((int)result["radius"]) + " mi of ZIP " + (string)result["zip"]
                    + " · " + Number(listings.Length) + " comparables shown, closest mileage first",
                Metrics = metrics.ToArray(), Listings = listings, PreserveListingOrder = true,
                Sections = new[] { new ChatMarketSection { Title = "Market evidence", Items = details.ToArray() } },
                Note = "Oy vey, listed asking prices give a clue,\nTitle and condition need checking by you.\nNo extra history premium was added to the view,\nOy, check each car and its fees before a deal goes through."
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
