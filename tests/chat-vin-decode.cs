using System;
using System.Collections;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using GTX.Common;
using GTX.Controllers;
using GTX.Models;

internal static class ChatVinDecodeTests
{
    private const string Vin = "1HGCM82633A004352";
    private const string Html = "<style>.avd-style{color:#123}</style><details class=\"avd-style\"><summary>Vehicle</summary>200 hp &amp; safety</details>";
    private const BindingFlags PrivateStatic = BindingFlags.Static | BindingFlags.NonPublic;
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    private static int Main(string[] args)
    {
        AppDomain.CurrentDomain.AssemblyResolve += (sender, e) =>
        {
            var path = Path.Combine(args[0], new AssemblyName(e.Name).Name + ".dll");
            return File.Exists(path) ? Assembly.LoadFrom(path) : null;
        };
        try { Run(); return 0; }
        catch (Exception ex) { Console.WriteLine(ex); return 1; }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Run()
    {
        // Block even an accidental regression into a live decoder/AI call.
        typeof(VinDecoderController).GetField("AutoDealerClient", PrivateStatic).SetValue(null, new HttpClient(new BlockNetworkHandler()));
        typeof(ChatController).GetField("OpenAiClient", PrivateStatic).SetValue(null, new HttpClient(new BlockNetworkHandler()));
        typeof(ChatController).GetField("VinLookupClient", PrivateStatic).SetValue(null, new HttpClient(new BlockNetworkHandler()));
        foreach (var text in new[] { "decode " + Vin, "decode VIN " + Vin, "decode VIN#" + Vin, "decode VIN: " + Vin,
            "  DeCoDe vin # " + Vin.ToLowerInvariant() + "  " })
            Check(Parse(text) == Vin, "Valid command was not normalized: " + text);

        foreach (var text in new[] { "decode", "decode VIN", "decode VIN#", "decode VIN SHORT", "decode VIN: " + Vin + " extra",
            "decode VIN 1HGCM82633A00435I", "decode VIN 1HGCM82633A00435O", "decode VIN 1HGCM82633A00435Q", "decode VIN " + Vin + "\nextra" })
            Check(Parse(text) == null, "Malformed command must be recognized and rejected: " + text);

        foreach (var text in new[] { null, "", "decodeVIN " + Vin, "Please decode VIN " + Vin, "Show me SUVs", "Vindecoder" })
        {
            var arguments = new object[] { text, null };
            Check(!(bool)typeof(ChatController).GetMethod("TryParseVinDecodeCommand", PrivateStatic).Invoke(null, arguments),
                "Ordinary text must keep its existing path: " + text);
        }

        FakeContext context;
        var controller = CreateController(out context);
        var denied = Send(controller, context, "decode VIN#" + Vin);
        Check(!denied.Success && denied.PrivateReply && denied.VinDecode == null && context.Response.StatusCode == 403,
            "Anonymous decoding must be intercepted and denied without AI or decoder calls");
        Check(context.CachePolicy.NoStore && context.Response.TrySkipIisCustomErrors, "Private errors must remain JSON with no-store");
        CheckFourLines(denied.Reply);

        controller = CreateController(out context);
        context.Session["CurrentRole"] = CommonUnit.Roles.Owner;
        Check(!Send(controller, context, "decode " + Vin).Success && context.Response.StatusCode == 403,
            "Legacy Owner session alone must not bypass the current SiteAccess permission");

        controller = CreateController(out context);
        var invalid = Send(controller, context, "decode VIN SHORT");
        Check(!invalid.Success && invalid.PrivateReply && context.Response.StatusCode == 400 && context.CachePolicy.NoStore,
            "Malformed VINs must be validated before AI/navigation");

        controller = CreateController(out context);
        var invalidToken = (JsonResult)controller.Message(new ChatBotRequest { Message = "decode " + Vin, ChatRequestToken = "wrong" }).GetAwaiter().GetResult();
        Check(!((ChatBotResponse)invalidToken.Data).Success && context.Response.StatusCode == 403,
            "Command must not bypass the existing request token");

        controller = CreateController(out context);
        controller.ModelState.AddModelError("Message", "Invalid request");
        Check(!Send(controller, context, "decode " + Vin).Success && context.Response.StatusCode == 400 && !context.CachePolicy.NoStore,
            "Model validation must run before command handling");

        controller = CreateController(out context);
        for (var index = 0; index < 21; index++) Send(controller, context, "decode VIN SHORT");
        Check(context.Response.StatusCode == 429, "Decode command must retain the chat rate limit");

        var custom = new SiteRoleModel { Name = "Decoder team", Permissions = new[] { "VinDecoder" } };
        Check(SiteAccess.Can(custom, MajordomeItem.VinDecoder), "A custom role with permission must remain eligible");
        custom.Permissions = new string[0];
        Check(!SiteAccess.Can(custom, MajordomeItem.VinDecoder), "Role name must not replace explicit permission");

        controller = CreateController(out context);
        var success = Reply(controller, new ContentResult { Content = Html, ContentType = "text/html" });
        Check(success.Success && success.PrivateReply && success.VinDecode.Vin == Vin && success.VinDecode.Html == Html,
            "Chat must preserve the exact built-in HTML and VIN");
        CheckFourLines(success.Reply);
        Check(success.Reply.Contains("View VIN report"), "Success text must point to the report modal button");
        Check(success.ResponseId == null && success.MarketReply == null && success.Vehicles == null,
            "Deterministic report must not enter AI or other result formats");

        foreach (var status in new[] { 400, 429, 502, 504, 500 })
        {
            controller = CreateController(out context);
            var error = Reply(controller, new HttpStatusCodeResult(status, "private-key / upstream details"));
            Check(!error.Success && error.PrivateReply && error.VinDecode == null && context.Response.StatusCode == (status == 500 ? 502 : status),
                "Incorrect chat mapping for HTTP " + status);
            Check(error.Reply.IndexOf("private-key", StringComparison.Ordinal) < 0 && error.Reply.IndexOf("upstream details", StringComparison.Ordinal) < 0,
                "Chat error must not disclose upstream details");
            CheckFourLines(error.Reply);
        }

        var fetched = Fetch(HttpStatusCode.OK, "text/html", Html) as ContentResult;
        Check(fetched != null && fetched.Content == Html && fetched.ContentType == "text/html", "Shared fetch must preserve HTML");
        CheckStatus(Fetch(HttpStatusCode.OK, "application/json", "{}"), 502);
        CheckStatus(Fetch(HttpStatusCode.OK, "text/html", " "), 502);
        CheckStatus(Fetch(HttpStatusCode.Unauthorized, "application/json", "private credentials"), 502);
        CheckStatus(Fetch((HttpStatusCode)429, "application/json", "quota details"), 429);

        var shared = typeof(VinDecoderController).GetMethod("DecodeHtmlAsync", PrivateStatic);
        Check(shared != null && shared.IsAssembly, "Existing action and chat must share an internal decode implementation");
        CheckStatus(((Task<ActionResult>)shared.Invoke(null, new object[] { "invalid" })).GetAwaiter().GetResult(), 400);
        var action = typeof(VinDecoderController).GetMethod("DecodeDataOneByAnyVin");
        Check(action.IsDefined(typeof(HttpPostAttribute)) && action.IsDefined(typeof(ValidateAntiForgeryTokenAttribute)),
            "Existing protected endpoint must retain POST and antiforgery");
        Check(typeof(VinDecoderController).IsDefined(typeof(RequireAdminRole), true), "Existing decoder authorization must remain");
        Check(((OutputCacheAttribute)Attribute.GetCustomAttribute(action, typeof(OutputCacheAttribute))).NoStore,
            "Existing decoder action must retain no-store");
        Check(BlockNetworkHandler.Attempts == 0, "Deterministic validation and permission checks must not request network access");
        Console.WriteLine("PASS: decode commands, validation, token/rate limits, permission denial, exact private HTML, safe error statuses and shared offline HTTP fetch");
    }

    private static string Parse(string message)
    {
        var arguments = new object[] { message, null };
        Check((bool)typeof(ChatController).GetMethod("TryParseVinDecodeCommand", PrivateStatic).Invoke(null, arguments), "Decode command not recognized");
        return (string)arguments[1];
    }

    private static ChatController CreateController(out FakeContext context)
    {
        var controller = new ChatController(null, null, null, null);
        context = new FakeContext();
        controller.ControllerContext = new ControllerContext(context, new RouteData(), controller);
        return controller;
    }

    private static ChatBotResponse Send(ChatController controller, FakeContext context, string message)
    {
        var token = ChatController.GetOrCreateRequestToken(context.Session);
        return (ChatBotResponse)((JsonResult)controller.Message(new ChatBotRequest { Message = message, ChatRequestToken = token }).GetAwaiter().GetResult()).Data;
    }

    private static ChatBotResponse Reply(ChatController controller, ActionResult result)
    {
        return (ChatBotResponse)((JsonResult)typeof(ChatController).GetMethod("CreateVinDecodeReply", PrivateInstance)
            .Invoke(controller, new object[] { Vin, result })).Data;
    }

    private static ActionResult Fetch(HttpStatusCode status, string type, string body)
    {
        using (var client = new HttpClient(new FakeHandler(status, type, body)))
            return ((Task<ActionResult>)typeof(VinDecoderController).GetMethod("FetchAutoDealerHtmlAsync", PrivateStatic)
                .Invoke(null, new object[] { client, Vin, "test-key.test-secret" })).GetAwaiter().GetResult();
    }

    private static void CheckStatus(ActionResult result, int expected)
    {
        var error = result as HttpStatusCodeResult;
        Check(error != null && error.StatusCode == expected, "Unexpected shared-fetch HTTP status");
    }

    private static void CheckFourLines(string reply) { Check(reply != null && reply.Split('\n').Length == 4, "Chatbot reply must use four compact lines"); }
    private static void Check(bool passed, string message) { if (!passed) throw new Exception(message); }

    private sealed class FakeContext : HttpContextBase
    {
        private readonly FakeRequest request = new FakeRequest();
        private readonly FakeResponse response = new FakeResponse();
        private readonly FakeSession session = new FakeSession();
        public readonly FakeCache CachePolicy = new FakeCache();
        public FakeContext() { response.CacheValue = CachePolicy; }
        public override HttpRequestBase Request { get { return request; } }
        public override HttpResponseBase Response { get { return response; } }
        public override HttpSessionStateBase Session { get { return session; } }
    }

    private sealed class FakeRequest : HttpRequestBase
    {
        private readonly HttpCookieCollection cookies = new HttpCookieCollection();
        public override HttpCookieCollection Cookies { get { return cookies; } }
        public override string UserAgent { get { return "offline decoder tests"; } }
        public override string UserHostAddress { get { return "127.0.0.1"; } }
    }

    private sealed class FakeResponse : HttpResponseBase
    {
        public FakeCache CacheValue;
        public override HttpCachePolicyBase Cache { get { return CacheValue; } }
        public override int StatusCode { get; set; }
        public override bool TrySkipIisCustomErrors { get; set; }
    }

    private sealed class FakeCache : HttpCachePolicyBase
    {
        public bool NoStore;
        public override void SetNoStore() { NoStore = true; }
    }

    private sealed class FakeSession : HttpSessionStateBase
    {
        private readonly Hashtable values = new Hashtable();
        private readonly string id = Guid.NewGuid().ToString("N");
        public override string SessionID { get { return id; } }
        public override object this[string name] { get { return values[name]; } set { values[name] = value; } }
    }

    private sealed class BlockNetworkHandler : HttpMessageHandler
    {
        internal static int Attempts;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref Attempts);
            throw new InvalidOperationException("Live network requests are blocked in this offline test.");
        }
    }

    private sealed class FakeHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode status;
        private readonly string type;
        private readonly string body;
        public FakeHandler(HttpStatusCode status, string type, string body) { this.status = status; this.type = type; this.body = body; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Check(request.Method == HttpMethod.Get && request.RequestUri.AbsoluteUri == "https://autodealer.dev/api/service/vin/" + Vin + "/html",
                "Shared fetch must use the fixed HTML endpoint");
            Check(request.Headers.Authorization.Scheme == "Bearer" && request.Headers.Authorization.Parameter == "test-key.test-secret",
                "Shared fetch must keep server-side bearer authentication");
            Check(request.Headers.Accept.ToString() == "text/html", "Shared fetch must request HTML");
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, type) });
        }
    }
}
