using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Mvc;
using GTX.Controllers;

internal static class VinDecoderApiTests
{
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
        const string html = "<style>.avd-style{color:#123}</style><details class=\"avd-style\"><summary>Vehicle</summary>200 hp</details>";
        var content = Fetch(HttpStatusCode.OK, "text/html", html) as ContentResult;
        Check(content != null && content.Content == html && content.ContentType == "text/html", "HTML and scoped styles must be preserved");
        CheckStatus(Fetch(HttpStatusCode.OK, "application/json", "{}"), 502);
        CheckStatus(Fetch(HttpStatusCode.OK, "text/html", ""), 502);
        CheckStatus(Fetch(HttpStatusCode.Unauthorized, "application/json", "private upstream error"), 502);
        CheckStatus(Fetch(HttpStatusCode.InternalServerError, "text/html", "upstream failure"), 502);
        CheckStatus(Fetch((HttpStatusCode)429, "application/json", "quota exceeded"), 429);

        var controller = (VinDecoderController)FormatterServices.GetUninitializedObject(typeof(VinDecoderController));
        foreach (var invalid in new[] { null, "", "SHORT", "1HGCM82633A00435!", "1HGCM82633A00435I" })
            CheckStatus(controller.DecodeDataOneByAnyVin(invalid).GetAwaiter().GetResult(), 400);

        var action = typeof(VinDecoderController).GetMethod("DecodeDataOneByAnyVin");
        Check(action.IsDefined(typeof(HttpPostAttribute)), "Decode must require POST");
        Check(action.IsDefined(typeof(ValidateAntiForgeryTokenAttribute)), "Decode must validate antiforgery token");
        Check(((OutputCacheAttribute)Attribute.GetCustomAttribute(action, typeof(OutputCacheAttribute))).NoStore, "Decode must disable response storage");
        Console.WriteLine("PASS: HTML passthrough, endpoint and authorization headers, error handling, quota handling, VIN validation, POST and antiforgery protection");
    }

    private static ActionResult Fetch(HttpStatusCode status, string type, string body)
    {
        using (var client = new HttpClient(new FakeHandler(status, type, body)))
        {
            var method = typeof(VinDecoderController).GetMethod("FetchAutoDealerHtmlAsync", BindingFlags.Static | BindingFlags.NonPublic);
            return ((Task<ActionResult>)method.Invoke(null, new object[] { client, "1HGCM82633A004352", "test-key.test-secret" })).GetAwaiter().GetResult();
        }
    }

    private static void CheckStatus(ActionResult result, int expected)
    {
        var error = result as HttpStatusCodeResult;
        Check(error != null && error.StatusCode == expected, "Unexpected HTTP status");
    }

    private static void Check(bool passed, string message)
    {
        if (!passed) throw new Exception(message);
    }

    private sealed class FakeHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode status;
        private readonly string type;
        private readonly string body;
        public FakeHandler(HttpStatusCode status, string type, string body) { this.status = status; this.type = type; this.body = body; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Check(request.Method == HttpMethod.Get, "Upstream must use GET");
            Check(request.RequestUri.AbsoluteUri == "https://autodealer.dev/api/service/vin/1HGCM82633A004352/html", "Incorrect HTML endpoint");
            Check(request.Headers.Authorization.Scheme == "Bearer" && request.Headers.Authorization.Parameter == "test-key.test-secret", "Incorrect authorization");
            Check(request.Headers.Accept.ToString() == "text/html", "Must request HTML");
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, type) });
        }
    }
}
