using GTX.Common;
using GTX.Controllers;
using Newtonsoft.Json.Linq;
using Services;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Runtime.Remoting.Messaging;
using System.Runtime.Remoting.Proxies;
using System.Runtime.Serialization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;

internal static class VinDecoderReportTests {
    private const string Vin = "1HGCM82633A004352";
    private const string Html = "<section><details open><summary>Vehicle</summary>Complete specifications</details></section>";
    private const string Xml = "<decoded_data><decoder_messages><service_provider>DataOne Software, Inc.</service_provider></decoder_messages><query_responses><query_response><us_market_data><us_styles><style name=\"EX\"><basic_data><year>2003</year><make>Honda</make><model>Accord</model><trim>EX</trim></basic_data></style></us_styles></us_market_data></query_response></query_responses></decoded_data>";
    private const string NeoJson = "{\"vin\":\"" + Vin + "\",\"year\":2003,\"make\":\"Honda\",\"model\":\"Accord\",\"trim\":\"EX\"}";
    internal static void Run() {
        foreach (var provider in new[] { "dataone", "neovin" }) {
            var handler = new Handler(provider);
            using (var client = new HttpClient(handler)) {
                var report = Fetch(client, provider);
                Check(handler.Calls == 1 && report.Vin == Vin && report.Provider == provider && report.Html == Html, "A report must use one provider request and retain trusted identity/HTML");
                Check(report.RawPayload == (provider == "dataone" ? Xml : NeoJson), "The original provider payload must be retained");
                Check(report.FormatterKey == provider + "-v1" && report.DecodedUtc.Value.Kind == DateTimeKind.Utc, "Provider formatting metadata must be explicit");
            }
            foreach (var timestamp in new JToken[] { new JObject(), new JArray(), JValue.CreateNull(), new JValue("invalid") }) {
                var broken = new Handler(provider) { Timestamp = timestamp };
                using (var client = new HttpClient(broken)) Expect(502, () => Fetch(client, provider));
            }
        }
        foreach (var origin in new[] { "http://research.example.test", "https://user:secret@research.example.test", "https://research.example.test?key=wrong", "https://research.example.test/#fragment" }) {
            var handler = new Handler("dataone");
            using (var client = new HttpClient(handler)) Expect(503, () => Fetch(client, "dataone", new Uri(origin)));
            Check(handler.Calls == 0, "Invalid origins must be rejected before sending credentials");
        }

        // This executable is created in its own temp directory by vin-decoder-api.ps1.
        var basePath = AppDomain.CurrentDomain.BaseDirectory;
        Check(Path.GetFileName(basePath.TrimEnd('\\', '/')).StartsWith("gtx-vin-api-", StringComparison.Ordinal), "Controller fixtures must use their isolated test directory");
        var settings = Path.Combine(basePath, "App_Data");
        Directory.CreateDirectory(settings);
        File.WriteAllText(Path.Combine(settings, "Services.config"), "<services><add key=\"AutoDealer:AppraisalBaseUrl\" value=\"https://research.example.test:4443/root/\"/><add key=\"AutoDealer:ApiKey\" value=\"test-key.test-secret\"/></services>");
        var clientField = typeof(VinDecoderController).GetField("AutoDealerClient", BindingFlags.Static | BindingFlags.NonPublic);
        var previousClient = clientField.GetValue(null);
        try {
            var inventory = new Inventory();
            var controller = Controller(inventory);
            foreach (var provider in new[] { "dataone", "neovin" }) {
                var handler = new Handler(provider);
                using (var client = new HttpClient(handler)) {
                    clientField.SetValue(null, client);
                    var result = (provider == "dataone" ? controller.DecodeDataOneByAnyVin(" " + Vin.ToLowerInvariant() + " ") : controller.DecodeMarketCheckByAnyVin(Vin)).GetAwaiter().GetResult() as ContentResult;
                    Check(Styled(result) && handler.Calls == 1, "Inventory decode must format, save and display the same single fetched report");
                    Check(Header(controller, "X-GTX-Vin-Saved") == "true" && inventory.Saved.ContainsKey(provider), "A successful inventory save must be reported accurately in IIS Classic mode");
                    inventory.Saved[provider].Html = "<p>Old snapshot styling</p>";
                    var loaded = controller.LoadSavedReport(Vin, provider) as ContentResult;
                    Check(Styled(loaded) && !loaded.Content.Contains("Old snapshot styling") && handler.Calls == 1, "Saved raw payloads must use the current shared formatter without decoding again");
                    if (provider == "neovin") Check(loaded.Content.Contains("2026-11-12 15:30 UTC"), "Reformatting must preserve the original decode time");
                }
            }
            Check(inventory.Saved.Count == 2, "Both provider reports must be addressable");
            inventory.FailSave = true;
            using (var client = new HttpClient(new Handler("neovin"))) {
                clientField.SetValue(null, client);
                Check(Styled(controller.DecodeMarketCheckByAnyVin(Vin).GetAwaiter().GetResult() as ContentResult), "A save failure must preserve the decoded report");
                Check(Header(controller, "X-GTX-Vin-Saved") == "failed", "A failed save must not claim success");
            }
            inventory.Stock = null;
            var outside = new Handler("neovin");
            using (var client = new HttpClient(outside)) {
                clientField.SetValue(null, client);
                var saves = inventory.SaveCalls;
                Check(Styled(controller.DecodeMarketCheckByAnyVin(Vin).GetAwaiter().GetResult() as ContentResult), "Non-inventory VIN decoding must remain available with the shared style");
                Check(Header(controller, "X-GTX-Vin-Saved") == "false" && inventory.SaveCalls == saves, "Non-inventory VINs must not be saved");
                Check((controller.LoadSavedReport(Vin, "neovin") as HttpStatusCodeResult)?.StatusCode == 404 && outside.Calls == 1, "Missing saved results must not trigger a provider request");
            }
            Check((controller.LoadSavedReport(Vin, "unknown") as HttpStatusCodeResult)?.StatusCode == 400, "Unknown provider names must be rejected");
        } finally { clientField.SetValue(null, previousClient); }
        Console.WriteLine("PASS: one-call raw report capture, strict metadata/origin validation, inventory-only autosave, both-provider load without billing, and preserved report after save failure");
    }

    private static VinDecoderReportDTO Fetch(HttpClient client, string provider, Uri origin = null) {
        return VinDecoderReportClient.FetchAsync(client, origin ?? new Uri("https://research.example.test:4443/root/"), "test-key.test-secret", Vin, provider).GetAwaiter().GetResult();
    }
    private static void Expect(int status, Action action) {
        try { action(); throw new Exception("Invalid report was accepted."); }
        catch (VinDecoderReportException ex) { Check(ex.StatusCode == status, "Incorrect safe decoder error status"); }
    }
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    private static string Header(VinDecoderController controller, string name) { return ((Response)controller.Response).SentHeaders[name]; }
    private static bool Styled(ContentResult result) { return result != null && result.Content.Contains("avd-report") && result.Content.Contains("Honda") && result.Content.Contains(Vin); }
    private static VinDecoderController Controller(Inventory inventory) {
        var controller = (VinDecoderController)FormatterServices.GetUninitializedObject(typeof(VinDecoderController));
        controller.InventoryService = (IInventoryService)inventory.GetTransparentProxy();
        controller.ControllerContext = new ControllerContext(new Context(), new RouteData(), controller);
        return controller;
    }
    private sealed class Context : HttpContextBase {
        private readonly Response response = new Response();
        public override HttpResponseBase Response => response;
    }
    private sealed class Response : HttpResponseBase {
        internal readonly NameValueCollection SentHeaders = new NameValueCollection();
        // IIS Classic rejects this collection even when AppendHeader is available.
        public override NameValueCollection Headers => throw new PlatformNotSupportedException("This operation requires IIS integrated pipeline mode.");
        public override void AppendHeader(string name, string value) { SentHeaders[name] = value; }
    }
    private sealed class Inventory : RealProxy {
        internal string Stock = "GTX0000001";
        internal bool FailSave;
        internal int SaveCalls;
        internal readonly Dictionary<string, VinDecoderReportDTO> Saved = new Dictionary<string, VinDecoderReportDTO>();
        internal Inventory() : base(typeof(IInventoryService)) { }
        public override IMessage Invoke(IMessage message) {
            var call = (IMethodCallMessage)message;
            try {
                object result;
                switch (call.MethodName) {
                    case "GetInventoryStockByVin": Check((string)call.Args[0] == Vin, "Inventory lookup must use normalized VIN"); result = Stock; break;
                    case "GetVinDecoderReport": Saved.TryGetValue((string)call.Args[1], out var report); result = report; break;
                    case "SaveVinDecoderReport":
                        SaveCalls++;
                        if (FailSave) throw new InvalidOperationException("simulated write failure");
                        Check((string)call.Args[0] == Stock, "The report must save to its resolved inventory stock");
                        var saved = (VinDecoderReportDTO)call.Args[1]; Saved[saved.Provider] = saved; result = null; break;
                    default: throw new Exception("Unexpected inventory operation: " + call.MethodName);
                }
                return new ReturnMessage(result, null, 0, call.LogicalCallContext, call);
            } catch (Exception ex) { return new ReturnMessage(ex, call); }
        }
    }
    private sealed class Handler : HttpMessageHandler {
        private readonly string provider;
        internal int Calls;
        internal JToken Timestamp = new JValue("2026-11-12T15:30:00Z");
        internal Handler(string provider) { this.provider = provider; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) {
            Calls++;
            Check(request.Headers.Authorization?.Parameter == "test-key.test-secret" && string.IsNullOrEmpty(request.RequestUri.Query), "Account credentials must stay in the authorization header");
            Check(request.RequestUri.Host == "research.example.test" && request.RequestUri.Port == 4443, "Use the configured AutoDealer origin");
            if (provider == "dataone") Check(request.Method == HttpMethod.Get && request.RequestUri.AbsolutePath == "/api/service/vin/" + Vin + "/report", "DataOne raw XML and HTML must come from one report request");
            else {
                Check(request.Method == HttpMethod.Post && request.RequestUri.AbsolutePath == "/api/service/neovin", "NeoVIN must use its one-call report endpoint");
                Check((string)JObject.Parse(await request.Content.ReadAsStringAsync())["Vin"] == Vin, "NeoVIN must receive the requested VIN");
            }
            var body = new JObject { ["vin"] = Vin, ["decodedAtUtc"] = Timestamp, ["html"] = Html };
            body[provider == "dataone" ? "rawXml" : "rawJson"] = provider == "dataone" ? Xml : NeoJson;
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body.ToString(), Encoding.UTF8, "application/json") };
        }
    }
}
