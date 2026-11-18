using GTX.Controllers;
using GTX.Common;
using GTX.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Runtime.Caching;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

internal static class ChatMarketAppraisalTests
{
    private const string Vin = "1HGCV1F53MA093993";
    private const string OtherVin = "1HGCV1F5XMA039817";
    private const string ToolName = "appraise_market_vehicle";
    private const string TestKey = "offline-key.offline-secret";
    private const BindingFlags Static = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
    private const BindingFlags Instance = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
    private static Type AppraisalType;
    private static int count;

    private static int Main(string[] args)
    {
        AppDomain.CurrentDomain.AssemblyResolve += (sender, e) =>
        {
            var path = Path.Combine(args[0], new AssemblyName(e.Name).Name + ".dll");
            return File.Exists(path) ? Assembly.LoadFrom(path) : null;
        };
        try { Run(); Console.WriteLine("PASS: " + count + " offline market-appraisal checks; no live AI, provider or database requests."); return 0; }
        catch (Exception ex) { Console.Error.WriteLine(ex.GetBaseException()); return 1; }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Run()
    {
        AppraisalType = typeof(ChatController).Assembly.GetType("GTX.Common.ChatAuctionAppraisal", true);
        foreach (var field in new[] { "OpenAiClient", "VinLookupClient" })
            typeof(ChatController).GetField(field, Static).SetValue(null, new HttpClient(new BlockNetworkHandler()));
        AppraisalType.GetField("Http", Static).SetValue(null, new HttpClient(new BlockNetworkHandler()));
        CheckSchema();
        CheckInputs();
        CheckGroundedInputs();
        CheckPresentation();
        CheckOpenAiTransport();
        CheckProgress();
        CheckHttp();
        Check(BlockNetworkHandler.Attempts == 0, "An offline check attempted an unmocked HTTP request");
    }

    private static void CheckSchema()
    {
        var tool = (JObject)AppraisalType.GetMethod("Tool", Static).Invoke(null, new object[] { ToolName });
        var schema = (JObject)tool["parameters"];
        var fields = (JObject)schema["properties"];
        Check((string)tool["name"] == ToolName && (string)tool["type"] == "function", "The composed market-appraisal tool must be available");
        Check(schema["required"] == null || !schema["required"].Values<string>().Contains("vin"), "Named vehicles must not require a VIN");
        Check(new[] { "vin", "year", "make", "model", "trim", "miles", "zip", "radius", "comparableCount" }.All(name => fields[name] != null), "Tool schema must accept the requested vehicle and search inputs");
        Check((int)fields["comparableCount"]["maximum"] == 10 && (int)fields["comparableCount"]["minimum"] == 1, "Provider work must be bounded to one through ten comparables");
        Check((string)fields["miles"]["type"] == "integer" && (int)fields["miles"]["maximum"] == 2000000 && (int)fields["radius"]["maximum"] == 500, "Odometer and geographic limits must stay distinct");
        Check((bool)schema["additionalProperties"] == false && fields["conditionPremium"] == null && fields["cleanCarfax"] == null, "The model must not request invented history-based adjustments");
        var legacy = (JObject)AppraisalType.GetMethod("Tool", Static).Invoke(null, new object[] { "get_market_price" });
        Check(legacy["parameters"]["required"].Values<string>().Contains("vin"), "Existing price-only VIN schema must stay compatible");
    }

    private static JObject Named() => JObject.Parse("{\"year\":2021,\"make\":\"Honda\",\"model\":\"Accord\",\"trim\":\"EX-L\"}");

    private static JObject Normalize(JObject input, string message) =>
        (JObject)typeof(ChatController).GetMethod("NormalizeMarketAppraisalArguments", Static).Invoke(null, new object[] { input, message });

    private static void CheckInputs()
    {
        var source = Named();
        source["miles"] = 100;
        source["radius"] = 10;
        source["comparableCount"] = 5;
        source["zip"] = "99999";
        var original = source.DeepClone();
        var input = Normalize(source, "Appraise a 2021 Honda Accord EX-L with 42k miles within 50 miles of ZIP 01234; show me 10 comparables.");
        Check(input != null && (int)input["miles"] == 42000 && (int)input["radius"] == 50, "Literal subject mileage and radius must override conflicting extraction independently");
        Check((string)input["zip"] == "01234" && (int)input["comparableCount"] == 10, "Explicit ZIP leading zero and requested comp count must survive");
        Check(JToken.DeepEquals(original, source), "Normalizing a request must not mutate the extracted input");
        input = Normalize(Named(), "Appraise a 2021 Honda Accord EX-L within 100 miles of ZIP 45040.");
        Check(input["miles"] == null && (int)input["radius"] == 100 && input["comparableCount"] == null, "A radius-only request must leave absent subject mileage/count to configured defaults");
        input = Normalize(new JObject { ["vin"] = OtherVin, ["miles"] = 42000 }, "Appraise VIN " + Vin.ToLowerInvariant() + " with 42,000 miles; show 10 comps.");
        Check((string)input["vin"] == Vin && input["year"] == null, "The literal user VIN must override a different model VIN");
        Check(Normalize(new JObject { ["vin"] = Vin }, "Appraise a car") == null, "A model-only VIN must never stand in for a missing user vehicle");
        var invented = Named(); invented["vin"] = Vin;
        input = Normalize(invented, "Appraise a 2021 Honda Accord EX-L");
        Check(input != null && input["vin"] == null && (int)input["year"] == 2021, "Named vehicles must discard a model-invented VIN");
        Check(Normalize(Named(), "Compare VIN " + Vin + " with " + OtherVin) == null, "Multiple subject VINs must be rejected before provider work");
        var zero = Named(); zero["miles"] = 0;
        Check((int)Normalize(zero, "Appraise a new 2021 Honda Accord EX-L with 0 miles")["miles"] == 0, "Zero odometer miles is an explicit valid value");

        foreach (var field in new[] { "miles", "radius", "comparableCount" })
            foreach (var bad in new JToken[] { new JValue(-1), new JValue(1.5m), new JValue("100"), JValue.CreateNull() })
            {
                var invalid = Named(); invalid[field] = bad;
                var normalized = Normalize(invalid, "Appraise a 2021 Honda Accord EX-L");
                Check(normalized == null || normalized[field] == null, "An invalid, unstated " + field + " must not become a provider input: " + bad.Type);
            }
        foreach (var entry in new[] { new { Field = "miles", Value = 2000001 }, new { Field = "radius", Value = 501 }, new { Field = "radius", Value = 0 }, new { Field = "comparableCount", Value = 11 }, new { Field = "comparableCount", Value = 0 } })
        {
            var invalid = Named(); invalid[entry.Field] = entry.Value;
            var normalized = Normalize(invalid, "Appraise a 2021 Honda Accord EX-L");
            Check(normalized == null || normalized[entry.Field] == null, "An out-of-range, unstated " + entry.Field + " must not become a provider input");
        }
        foreach (var field in new[] { "make", "model" })
        {
            var invalid = Named(); invalid[field] = " ";
            Check(Normalize(invalid, "Appraise a car") == null, "Missing " + field + " must request clarification");
            invalid[field] = "Honda,Toyota";
            Check(Normalize(invalid, "Appraise a car") == null, "Comma filters must not widen a single-subject lookup");
        }
        var year = Named(); year["year"] = DateTime.UtcNow.Year + 3;
        Check(Normalize(year, "Appraise a " + (DateTime.UtcNow.Year + 3) + " Honda Accord EX-L") == null, "Unsupported future years must be rejected even when the text supplies them");
        var zip = Named(); zip["zip"] = "1234";
        var normalizedZip = Normalize(zip, "Appraise a 2021 Honda Accord EX-L");
        Check(normalizedZip == null || normalizedZip["zip"] == null, "An invented malformed ZIP must be rejected or omitted");
        var extra = Named(); extra["history"] = "Clean Carfax"; extra["url"] = "https://attacker.test"; extra["resaleUsd"] = 100000;
        input = Normalize(extra, "Appraise a 2021 Honda Accord EX-L with a clean Carfax");
        Check(input["history"] == null && input["url"] == null && input["resaleUsd"] == null, "Unsupported model fields must not reach the provider");
        input = Normalize(Named(), "Appraise a 2021 Honda Accord EX-L; show 30 comparables");
        Check((int)input["comparableCount"] == 10, "A large literal request must remain bounded to ten results");
    }

    private static void CheckGroundedInputs()
    {
        Check(Normalize(Named(), "Appraise my car") == null, "A complete vehicle invented by the model must not become a paid provider search");
        foreach (var text in new[] {
            "Appraise a Honda Accord EX-L", "Appraise a 2021 Accord EX-L", "Appraise a 2021 Honda EX-L",
            "Appraise a 2021 Honda Accord", "Appraise a 2022 Honda Accord EX-L",
            "Appraise a 2021 Honda Camry EX-L", "Appraise a 2021 Honda Accord Touring"
        }) Check(Normalize(Named(), text) == null, "Every extracted vehicle component must be supported by the user's words: " + text);

        var untrimmed = Named(); untrimmed.Remove("trim");
        Check(Normalize(untrimmed, "Appraise a 2021 Honda Accord") != null, "An actual named vehicle must not need an unstated trim");
        var guessed = Named(); guessed["miles"] = 100; guessed["zip"] = "90210"; guessed["radius"] = 50;
        var input = Normalize(guessed, "Appraise a 2021 Honda Accord EX-L within 100 miles of ZIP 45040");
        Check(input != null && input["miles"] == null && (int)input["radius"] == 100 && (string)input["zip"] == "45040",
            "A geographic 100-mile search must not become an invented 100-mile subject odometer");
        input = Normalize(guessed, "Appraise a 2021 Honda Accord EX-L");
        Check(input != null && input["miles"] == null && input["zip"] == null && input["radius"] == null,
            "Unstated mileage, ZIP and radius must remain omitted even if the model guesses valid values");
        guessed["miles"] = 999; guessed["comparableCount"] = 30;
        input = Normalize(guessed, "Appraise a 2021 Honda Accord EX-L with 42k miles; show 30 comparables");
        Check(input != null && (int)input["miles"] == 42000 && (int)input["comparableCount"] == 10,
            "Literal 42k mileage and a requested thirty comparables must override extraction and safely cap to ten");
        input = Normalize(guessed, "Appraise a 2021 Honda Accord EX-L with 42k miles; show five comparables");
        Check(input != null && (int)input["comparableCount"] == 5, "An explicit word count must override the model's different comparable count");
        foreach (var text in new[] {
            "Appraise a 2021 Honda Accord EX-L with 2,000,001 miles",
            "Appraise a 2021 Honda Accord EX-L within 501 miles of ZIP 45040",
            "Appraise a 2021 Honda Accord EX-L within -1 miles of ZIP 45040",
            "Appraise a 2021 Honda Accord EX-L within 1.5 miles of ZIP 45040",
            "Appraise a 2021 Honda Accord EX-L under 42k miles",
            "Appraise a 2021 Honda Accord EX-L; show 0 comparables"
        }) Check(Normalize(Named(), text) == null, "Invalid explicit user limits must be rejected rather than silently defaulted: " + text);
    }

    private static JObject Fixture()
    {
        var rows = new JArray();
        for (var index = 0; index < 12; index++) rows.Add(new JObject {
            ["vin"] = index == 0 ? Vin : OtherVin, ["vehicle"] = "2021 Honda Accord EX-L #" + index,
            ["miles"] = 42000 + index * 100, ["askingPriceUsd"] = index == 1 ? JValue.CreateNull() : new JValue(25563 - index * 100),
            ["daysListed"] = index == 1 ? JValue.CreateNull() : new JValue(index == 0 ? 0 : 102),
            ["distanceMiles"] = index == 0 ? 0 : index, ["dealerName"] = "Dealer " + index, ["city"] = "Test City", ["state"] = "OH",
            ["url"] = index == 2 ? "javascript:alert(1)" : index == 3 ? "https://user:secret@example.test/vehicle" : "https://dealer.example.test/car/" + index
        });
        return new JObject {
            ["vehicle"] = "2021 Honda Accord EX-L", ["vin"] = Vin, ["usedReferenceVin"] = true,
            ["miles"] = 42000, ["zip"] = "01234", ["radius"] = 50, ["marketCheckRetailUsd"] = 25444,
            ["msrpUsd"] = 32305, ["listingCount"] = 186,
            ["statistics"] = new JObject {
                ["sampledListingCount"] = 50, ["pricedSampleCount"] = 46, ["meanPriceUsd"] = 23255, ["medianPriceUsd"] = 23655,
                ["p25PriceUsd"] = 21987, ["p75PriceUsd"] = 24999, ["medianDaysListed"] = 97.5m
            },
            ["comparables"] = rows
        };
    }

    private static ChatMarketReply Format(JObject body) =>
        (ChatMarketReply)AppraisalType.GetMethod("FormatMarketAppraisalReply", Static).Invoke(null, new object[] { body });

    private static void CheckPresentation()
    {
        var body = Fixture();
        var original = body.DeepClone();
        var culture = Thread.CurrentThread.CurrentCulture;
        ChatMarketReply reply;
        try { Thread.CurrentThread.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR"); reply = Format(body); }
        finally { Thread.CurrentThread.CurrentCulture = culture; }
        Check(reply.PreserveListingOrder && reply.Listings.Length == 10, "Appraisal must keep bounded provider-ranked comparable order");
        Check(reply.Listings[0].Vehicle.EndsWith("#0") && reply.Listings[9].Vehicle.EndsWith("#9"), "Presentation must not resort comparables by asking price");
        Check(reply.Listings[0].Vin == Vin && reply.Listings[0].DaysListed == "0" && reply.Listings[0].Distance == "0", "VIN, zero days listed and zero distance must remain available to the UI");
        Check(reply.Listings[1].Ask == null && reply.Listings[1].DaysListed == null, "Missing price and days must stay unavailable, not become zero");
        Check(reply.Listings[2].Url == null && reply.Listings[3].Url == null && reply.Listings[0].Url.StartsWith("https://dealer.example.test/"), "Only safe credential-free HTTPS listing URLs may reach the UI");
        Check(reply.Listings[0].Ask == "$25,563" && reply.Listings[0].Odometer == "42,000", "Display dollars and miles must remain US-formatted under another host culture");
        Check(reply.Metrics.Single(metric => metric.IsPrimary).Value == "$25,444", "The provider prediction must remain the primary metric");
        Check(reply.Metrics.Single(metric => metric.Label == "Original MSRP").Value == "$32,305" && reply.Metrics.Single(metric => metric.Label == "Sample median ask").Value == "$23,655", "MSRP and sampled median must retain distinct factual labels");
        Check(reply.Metrics.Single(metric => metric.Label == "Sample middle 50%").Value == "$21,987–$24,999", "Quartiles must retain the returned range");
        var evidence = string.Join("\n", reply.Sections.SelectMany(section => section.Items));
        Check(evidence.Contains("50 sampled listings") && evidence.Contains("46 with asking prices") && evidence.Contains("186 matching active listings"), "Total matches and fetched priced sample must not be conflated");
        Check(evidence.Contains("Reference VIN: " + Vin) && evidence.Contains("not your vehicle's VIN"), "A named-vehicle reference VIN must not masquerade as the user's car");
        Check(evidence.Contains("97.5"), "Fractional median days must not be rounded into a fabricated integer");
        Check(reply.Context.Contains("42,000 miles") && reply.Context.Contains("50 mi of ZIP 01234") && reply.Context.Contains("10 comparables"), "Request mileage, geography and displayed result count must stay explicit");
        Check(string.IsNullOrEmpty(reply.Note), "Successful appraisals must omit the removed disclaimer verse");
        Check(!reply.Metrics.Any(metric => metric.Label.IndexOf("trade", StringComparison.OrdinalIgnoreCase) >= 0 || metric.Label.IndexOf("private", StringComparison.OrdinalIgnoreCase) >= 0), "The report must not fabricate trade-in or private-party values");
        Check(JToken.DeepEquals(original, body), "Presentation must not change provider evidence");
        var serialized = JObject.Parse(JsonConvert.SerializeObject(reply));
        Check((string)serialized["Listings"][0]["Vin"] == Vin && (string)serialized["Listings"][0]["DaysListed"] == "0", "VIN and days must actually be serialized for the browser");

        var actual = Fixture(); actual["usedReferenceVin"] = false; actual["usedDefaultMileage"] = true; actual.Remove("msrpUsd"); actual["comparables"] = new JArray();
        var sparse = Format(actual);
        Check(!sparse.Metrics.Any(metric => metric.Label == "Original MSRP") && sparse.Listings.Length == 0, "Missing MSRP and comparables must remain missing");
        Check(sparse.Context.Contains("assumed miles") && sparse.Sections[0].Items.Any(item => item.Contains("Subject VIN:") && item.Contains("excluded")), "Default mileage and actual-subject exclusion must be identified");
        Check(sparse.Sections[0].Items.Any(item => item.Contains("No matching comparable")), "An empty result must not claim ten comparable listings");
        foreach (var value in new JToken[] { JValue.CreateNull(), new JValue(0), new JValue(-1) }) {
            var invalid = Fixture(); invalid["marketCheckRetailUsd"] = value;
            Throws<JsonSerializationException>(() => Format(invalid), "Invalid provider estimates must not be displayed");
        }
    }

    private static void CheckProgress()
    {
        var id = Guid.NewGuid();
        var key = "GTX:ChatMarketProgress:" + id.ToString("N");
        typeof(ChatProgressController).GetMethod("StartMarketAppraisal", Static).Invoke(null, new object[] { (Guid?)id });
        Check((string)MemoryCache.Default.Get(key) == "market-appraisal", "The backend lookup must publish its distinct MarketCheck AI stage");
        typeof(ChatProgressController).GetMethod("Clear", Static).Invoke(null, new object[] { (Guid?)id });
        Check(MemoryCache.Default.Get(key) == null, "Completion and failure cleanup must remove the progress stage");
        typeof(ChatProgressController).GetMethod("StartMarketAppraisal", Static).Invoke(null, new object[] { null });
    }

    private static void CheckOpenAiTransport()
    {
        var previousContext = HttpContext.Current;
        try {
            HttpContext.Current = new HttpContext(new HttpRequest("", "https://offline.example.test/", ""), new HttpResponse(new StringWriter()));
            var snapshot = new ServiceConfigurationSnapshot();
            var settings = new ReadOnlyDictionary<string, string>(new Dictionary<string, string> {
                ["OpenAI:ResponsesUrl"] = "https://ai.example.test/v1/responses", ["OpenAI:ChatTimeoutSeconds"] = "5",
                ["OpenAI:ChatModel"] = "obsolete-model-override"
            });
            typeof(ServiceConfigurationSnapshot).GetProperty("Values").SetValue(snapshot, settings, null);
            HttpContext.Current.Items["GTX.ServiceConfiguration"] = snapshot;
            var controller = new ChatController(null, null, null, null);
            var method = typeof(ChatController).GetMethod("PostOpenAiAsync", Instance);
            foreach (var budget in new[] { 100, 250, 300, 400, 1800 }) {
                var handler = new FakeHandler(HttpStatusCode.OK, "{\"id\":\"resp_offline\",\"output\":[]}");
                using (var http = new HttpClient(handler)) {
                    typeof(ChatController).GetField("OpenAiClient", Static).SetValue(null, http);
                    var payload = new JObject {
                        ["model"] = "obsolete-caller-model", ["input"] = "Only this current request",
                        ["max_output_tokens"] = budget, ["reasoning"] = new JObject { ["effort"] = "high" }, ["store"] = false,
                        ["temperature"] = 0.7m, ["top_p"] = 0.9m
                    };
                    var response = ((Task<JObject>)method.Invoke(controller, new object[] { "offline-openai-key", payload })).GetAwaiter().GetResult();
                    Check(handler.Calls == 1 && handler.Uri.AbsoluteUri == "https://ai.example.test/v1/responses" && handler.Authorization == "Bearer offline-openai-key",
                        "Every Rabinovich OpenAI call must retain its configured endpoint and header authentication");
                    Check((string)handler.Input["model"] == "gpt-5-mini" && (string)handler.Input["reasoning"]?["effort"] == "low",
                        "Greeting, parsing, retail and chat payloads must use the selected GPT-5 mini model and low reasoning effort");
                    Check((int)handler.Input["max_output_tokens"] >= 1500 && (budget < 1500 || (int)handler.Input["max_output_tokens"] >= budget),
                        "Small caller budgets need enough room for reasoning and visible output without shrinking larger budgets");
                    Check(handler.Input["temperature"] == null && handler.Input["top_p"] == null,
                        "GPT-5 mini transport must remove unsupported legacy sampling controls");
                    Check((string)handler.Input["input"] == "Only this current request" && (bool)handler.Input["store"] == false && (string)response["id"] == "resp_offline",
                        "Model normalization must preserve the fresh request, privacy choice and usable API response");
                }
            }
        } finally { HttpContext.Current = previousContext; }
    }

    private static void CheckHttp()
    {
        var previousContext = HttpContext.Current;
        try {
            HttpContext.Current = new HttpContext(new HttpRequest("", "https://offline.example.test/", ""), new HttpResponse(new StringWriter()));
            // Request-scoped profile prevents any database access when defaults are applied.
            HttpContext.Current.Items["GTX.SiteProfile"] = new SiteProfileModel { ZipCode = "01234", ResearchRadiusMiles = 50 };
            var handler = new FakeHandler(HttpStatusCode.OK, Fixture().ToString(Formatting.None));
            var reply = Fetch(handler, "{\"year\":2021,\"make\":\"Honda\",\"model\":\"Accord\",\"trim\":\"EX-L\",\"miles\":42000,\"comparableCount\":10}");
            Check(handler.Calls == 1 && handler.Uri.AbsolutePath == "/api/service/price-research/market-appraisal" && handler.Uri.Query.Length == 0, "The composed lookup must use one private backend POST without credentials in its URL");
            Check(handler.Authorization == "Bearer " + TestKey && handler.ExpectContinue == false, "The backend key stays in Authorization and avoids hosted IIS 100-continue");
            Check((string)handler.Input["zip"] == "01234" && (int)handler.Input["radius"] == 50 && (int)handler.Input["miles"] == 42000, "Profile defaults must preserve actual mileage and apply only omitted geography");
            Check(reply.Listings.Length == 10 && reply.PreserveListingOrder, "HTTP integration must return the rich report rather than a price-only response");
            Check(!handler.Input.ToString().Contains(TestKey), "The backend key must not enter user/provider request data");
            var explicitScope = new FakeHandler(HttpStatusCode.OK, Fixture().ToString(Formatting.None));
            Fetch(explicitScope, "{\"vin\":\"" + Vin + "\",\"miles\":0,\"zip\":\"90210\",\"radius\":25}");
            Check((string)explicitScope.Input["zip"] == "90210" && (int)explicitScope.Input["radius"] == 25 && (int)explicitScope.Input["miles"] == 0, "Explicit location and zero mileage must not be replaced by profile defaults");

            foreach (var status in new[] { HttpStatusCode.BadRequest, (HttpStatusCode)429, HttpStatusCode.ServiceUnavailable, HttpStatusCode.Found }) {
                var failure = new FakeHandler(status, "MarketCheck offline-secret upstream/private/path");
                var error = Fetch(failure, "{\"vin\":\"" + Vin + "\"}");
                Check(failure.Calls == 1 && error.Listings.Length == 0 && error.Metrics.Length == 0, "A failed lookup must not produce a successful estimate or retry hidden calls");
                Check(!error.Note.Contains("offline-secret") && !error.Note.Contains("upstream/private/path"), "Provider errors must not leak credentials or infrastructure paths");
                CheckFourLines(error.Note);
                if ((int)status == 429) Check(error.Note.Contains("MarketCheck") && error.Note.Contains("quota"), "Provider quota errors must be actionable");
            }
            var stoppedMcp = Fetch(new FakeHandler(HttpStatusCode.ServiceUnavailable,
                "{\"message\":\"The MarketCheck MCP service is not running or cannot be reached. Start the configured local MCP service and try again.\"}"), "{\"vin\":\"" + Vin + "\"}");
            CheckFourLines(stoppedMcp.Note);
            Check(stoppedMcp.Note.Contains("MCP service") && stoppedMcp.Note.Contains("Start the configured service")
                && stoppedMcp.Listings.Length == 0 && stoppedMcp.Metrics.Length == 0,
                "An unavailable local MCP service must name the missing dependency and explain how to recover without fabricating an appraisal");
            var malformed = Fetch(new FakeHandler(HttpStatusCode.OK, "{"), "{\"vin\":\"" + Vin + "\"}");
            CheckFourLines(malformed.Note);
            Check(malformed.Listings.Length == 0, "Malformed upstream JSON must return a safe private error without fabricated listings");
        } finally { HttpContext.Current = previousContext; }
    }

    private static ChatMarketReply Fetch(FakeHandler handler, string input)
    {
        using (var http = new HttpClient(handler)) {
            AppraisalType.GetField("Http", Static).SetValue(null, http);
            var client = AppraisalType.GetConstructor(Instance, null, new[] { typeof(Uri), typeof(string) }, null)
                .Invoke(new object[] { new Uri("https://research.example.test/api/service/price-research/"), TestKey });
            return ((Task<ChatMarketReply>)AppraisalType.GetMethod("ReplyAsync", Instance)
                .Invoke(client, new object[] { input, ToolName })).GetAwaiter().GetResult();
        }
    }

    private static void CheckFourLines(string text) => Check(text != null && text.Split('\n').Length == 4 && text.Split('\n').All(line => !string.IsNullOrWhiteSpace(line)), "New chatbot narrative must remain four compact lines");
    private static void Check(bool ok, string message) { if (!ok) throw new Exception(message); count++; }
    private static void Throws<T>(Action action, string message) where T : Exception
    {
        try { action(); }
        catch (Exception error) { if (error.GetBaseException() is T) { count++; return; } throw; }
        throw new Exception(message);
    }

    private sealed class BlockNetworkHandler : HttpMessageHandler
    {
        internal static int Attempts;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) { Attempts++; throw new Exception("Live network is forbidden in offline appraisal tests"); }
    }

    private sealed class FakeHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode status;
        private readonly string body;
        internal int Calls;
        internal Uri Uri;
        internal string Authorization;
        internal bool? ExpectContinue;
        internal JObject Input;
        internal FakeHandler(HttpStatusCode status, string body) { this.status = status; this.body = body; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Calls++; Uri = request.RequestUri; Authorization = request.Headers.Authorization.ToString(); ExpectContinue = request.Headers.ExpectContinue;
            Input = JObject.Parse(await request.Content.ReadAsStringAsync());
            if (request.Method != HttpMethod.Post) throw new Exception("Expected a backend POST");
            return new HttpResponseMessage(status) { Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json") };
        }
    }
}
