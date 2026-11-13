using GTX.Common;
using GTX.Controllers;
using Newtonsoft.Json.Linq;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;

internal static class OpenAiModelsTests {
    private const string ApiKey = "synthetic-model-catalog-key";
    private const string ProviderSecret = "provider-error-body-must-stay-private";

    private static int Main(string[] args) {
        AppDomain.CurrentDomain.AssemblyResolve += (sender, e) => {
            var path = Path.Combine(args[0], new AssemblyName(e.Name).Name + ".dll");
            return File.Exists(path) ? Assembly.LoadFrom(path) : null;
        };
        try { Run().GetAwaiter().GetResult(); return 0; }
        catch (Exception ex) { Console.WriteLine(ex); return 1; }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static async Task Run() {
        var identifiers = new[] {
            "whisper-1", "gpt-image-1", "ft:gpt-4.1-mini:org:custom:123", "text-embedding-3-large",
            "gpt-4.1-mini", "dall-e-3", "unknown-project-model-v42", "o3", "gpt-4.1-mini", "Z-custom", "z-custom"
        };
        var data = new JArray(identifiers.Select(id => new JObject { ["id"] = id, ["object"] = "model" }));
        data.Add(new JObject { ["id"] = 42 });
        data.Add(new JObject());
        data.Add(new JObject { ["id"] = "" });
        data.Add(new JObject { ["id"] = "unsafe\nmodel" });
        data.Add(new JObject { ["id"] = new string('m', 2049) });
        data.Add("invalid-row");
        var handler = new Handler((request, token) => Task.FromResult(Response(HttpStatusCode.OK,
            new JObject { ["object"] = "list", ["data"] = data }.ToString())));
        using (var client = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan }) {
            var models = await new OpenAiModelCatalog(client).ListAsync(" " + ApiKey + " ");
            var expected = identifiers.Distinct(StringComparer.Ordinal).OrderBy(id => id, StringComparer.OrdinalIgnoreCase).ThenBy(id => id, StringComparer.Ordinal).ToArray();
            Check(models.SequenceEqual(expected), "Every accessible model family must be returned, deduplicated and sorted without capability filtering");
            Check(!models.Contains("old-saved-model"), "The server must not invent or force a model that the API did not return");
            Check(handler.Calls == 1, "Listing models must make exactly one metadata request and no generation calls");
        }
        handler = new Handler((request, token) => Task.FromResult(Response(HttpStatusCode.OK, "{\"data\":[]}")));
        using (var client = new HttpClient(handler))
            Check((await new OpenAiModelCatalog(client).ListAsync(ApiKey)).Length == 0, "An empty accessible model list must remain empty without a fabricated default");

        foreach (var status in new[] { HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, (HttpStatusCode)429, HttpStatusCode.InternalServerError, HttpStatusCode.Redirect }) {
            handler = new Handler((request, token) => {
                var response = Response(status, new JObject { ["error"] = new JObject { ["message"] = ProviderSecret + " " + ApiKey } }.ToString());
                response.Headers.Location = new Uri("https://untrusted.example.test/" + ProviderSecret);
                return Task.FromResult(response);
            });
            using (var client = new HttpClient(handler)) {
                await Error(new OpenAiModelCatalog(client), ApiKey);
                Check(handler.Calls == 1, "HTTP errors and redirects must not trigger a second authenticated request");
            }
        }

        foreach (var body in new[] { "not-json " + ProviderSecret, "{}", "{\"data\":null}", "{\"data\":\"invalid\"}" }) {
            handler = new Handler((request, token) => Task.FromResult(Response(HttpStatusCode.OK, body)));
            using (var client = new HttpClient(handler)) await Error(new OpenAiModelCatalog(client), ApiKey);
        }
        handler = new Handler((request, token) => { throw new HttpRequestException(ProviderSecret + " " + ApiKey); });
        using (var client = new HttpClient(handler)) await Error(new OpenAiModelCatalog(client), ApiKey);

        handler = new Handler((request, token) => { throw new InvalidOperationException("Invalid API keys must never reach a transport."); });
        using (var client = new HttpClient(handler)) {
            var catalog = new OpenAiModelCatalog(client);
            foreach (var invalid in new[] { null, "", "  ", "\t", "unsafe\r\nheader", "internal whitespace", new string('k', 2049) }) await Error(catalog, invalid);
            Check(handler.Calls == 0, "Empty or header-invalid keys must be rejected before a request is sent");
        }

        var cancellationObserved = false;
        handler = new Handler((request, token) => {
            Check(token.CanBeCanceled, "The metadata request must have a finite cancellation deadline");
            var completion = new TaskCompletionSource<HttpResponseMessage>();
            token.Register(() => { cancellationObserved = true; completion.TrySetCanceled(); });
            return completion.Task;
        });
        using (var client = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan }) {
            var clock = Stopwatch.StartNew();
            var request = Error(new OpenAiModelCatalog(client), ApiKey);
            var finished = await Task.WhenAny(request, Task.Delay(TimeSpan.FromSeconds(15)));
            Check(object.ReferenceEquals(finished, request), "Model loading must finish within its own finite deadline even when the shared client has no timeout");
            await request;
            Check(cancellationObserved && clock.Elapsed >= TimeSpan.FromSeconds(8), "The ten-second model-list deadline did not cancel the stalled transport");
        }

        var controller = typeof(ServicesController);
        Check(controller.GetCustomAttributes(typeof(RequireAdminRole), true).Cast<RequireAdminRole>().Single().RequireMajordome,
            "Model discovery must inherit Majordome-only service access");
        var action = controller.GetMethod("OpenAiModels");
        Check(action != null && action.IsDefined(typeof(HttpPostAttribute), true), "Model discovery must accept the current key through POST");
        Check(action.IsDefined(typeof(ValidateAntiForgeryTokenAttribute), true), "Model discovery must require an anti-forgery token");
        Check(!action.GetCustomAttributes(typeof(ValidateInputAttribute), true).Cast<ValidateInputAttribute>().Single().EnableValidation,
            "Model discovery must accept raw credentials without putting them into a URL");
        var offlineController = (ServicesController)FormatterServices.GetUninitializedObject(controller);
        var context = new FakeContext();
        offlineController.ControllerContext = new ControllerContext(context, new RouteData(), offlineController);
        // Header-invalid keys are rejected before transport, so this exercises the real endpoint without a live request.
        var endpointResult = await offlineController.OpenAiModels(ApiKey + "\r\n" + ProviderSecret) as JsonResult;
        Check(endpointResult != null && endpointResult.JsonRequestBehavior == JsonRequestBehavior.DenyGet,
            "Model discovery must return POST-only JSON");
        var endpointPayload = JObject.FromObject(endpointResult.Data);
        Check((bool)endpointPayload["ok"] == false && context.ResponseValue.CacheValue.NoStore,
            "Even failed model discovery must send a no-store response");
        Check(!((string)endpointPayload["message"]).Contains(ApiKey) && !((string)endpointPayload["message"]).Contains(ProviderSecret),
            "The controller response must not echo submitted credentials");
        Console.WriteLine("PASS: all accessible model families, fixed official GET/Bearer/no-key URL, deduplication, safe HTTP/parse/transport failures, invalid-key no-call, finite cancellation, protected POST and actual no-store controller failures");
    }

    private static async Task Error(OpenAiModelCatalog catalog, string key) {
        try { await catalog.ListAsync(key); }
        catch (OpenAiModelCatalogException ex) {
            Check(!string.IsNullOrWhiteSpace(ex.Message), "Model-list errors must have a usable safe message");
            Check(!ex.Message.Contains(ApiKey) && !ex.Message.Contains(ProviderSecret), "Model-list errors must never echo keys or provider response bodies");
            return;
        }
        throw new InvalidOperationException("The model catalog accepted an invalid input or unsuccessful provider response.");
    }
    private static HttpResponseMessage Response(HttpStatusCode status, string body) {
        return new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
    }
    private static void Check(bool condition, string message) {
        if (!condition) throw new InvalidOperationException(message);
    }
    private sealed class Handler : HttpMessageHandler {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send;
        public int Calls;
        public Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) { this.send = send; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
            Calls++;
            Check(request.Method == HttpMethod.Get && request.RequestUri.AbsoluteUri == "https://api.openai.com/v1/models", "Model discovery must use only the fixed official metadata endpoint");
            Check(request.Headers.Authorization?.Scheme == "Bearer" && request.Headers.Authorization.Parameter == ApiKey, "The supplied key must remain in the server-side Bearer header");
            Check(request.Content == null && !request.RequestUri.AbsoluteUri.Contains(ApiKey), "Model metadata requests must never put the API key into the URL or body");
            return send(request, cancellationToken);
        }
    }
    private sealed class FakeContext : HttpContextBase {
        public readonly FakeResponse ResponseValue = new FakeResponse();
        public override HttpResponseBase Response { get { return ResponseValue; } }
    }
    private sealed class FakeResponse : HttpResponseBase {
        public readonly FakeCache CacheValue = new FakeCache();
        public override HttpCachePolicyBase Cache { get { return CacheValue; } }
    }
    private sealed class FakeCache : HttpCachePolicyBase {
        public bool NoStore;
        public override void SetNoStore() { NoStore = true; }
    }
}
