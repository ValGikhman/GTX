using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Caching;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using GTX;
using GTX.Common;
using GTX.Controllers;
using GTX.Models;
using Services;

public static class InventoryIsolationTests
{
    private static string binPath;
    private static Inventory cached;
    private static readonly List<string> searchTokens = new List<string>();
    private static int assertions;

    public static int Main(string[] args)
    {
        if (args.Length != 1) { Console.Error.WriteLine("Expected GTX bin directory."); return 1; }
        binPath = args[0];
        AppDomain.CurrentDomain.AssemblyResolve += delegate(object sender, ResolveEventArgs e) {
            var path = Path.Combine(binPath, new AssemblyName(e.Name).Name + ".dll");
            return File.Exists(path) ? Assembly.LoadFrom(path) : null;
        };
        try { Run(); return 0; }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Run()
    {
        var all = Enumerable.Range(1, 200).Select(i => new GTX.Models.GTX {
            Stock = "STOCK" + i.ToString("D3"), Make = i <= 11 ? "ELEVEN" : "OTHER",
            Model = "Roadcar", Year = 2020, VehicleType = i <= 128 ? "SUV" : "SEDAN",
            InternetPrice = 15000, Mileage = 40000, Transmission = "A", Cylinders = 4,
            FuelType = "GAS", DriveTrain = "AWD", Body = "WAGON", Color = "BLUE"
        }).ToArray();
        cached = new Inventory { All = all, Vehicles = all, Published = DateTime.UtcNow, Title = "Cached" };
        var cacheKey = "GTX:InventoryIsolationTests:" + Guid.NewGuid().ToString("N");
        try {
            AppCache.GetOrCreate(cacheKey, () => cached, 60);
            var a = Create(AppCache.GetOrCreate<Inventory>(cacheKey, () => null, 60));
            var b = Create(AppCache.GetOrCreate<Inventory>(cacheKey, () => null, 60));
            Check(!Object.ReferenceEquals(a.Model.Inventory, b.Model.Inventory), "Requests must have separate inventory wrappers.");
            Check(Object.ReferenceEquals(a.Model.Inventory.All, all), "Cached vehicle data should be reused.");
            Check(a.Model.Inventory.Published == cached.Published, "Request wrappers must retain publication time.");
            Check(new Inventory().CreateForRequest().All.Length == 0, "Missing inventory data must produce an empty request list.");
            a.All("ELEVEN", null, null, null);
            Count(a, 11, "Filtered All"); Count(b, 200, "Other visitor before menu");
            b.Index();
            Count(b, 200, "Other visitor menu");
            b.All(null, null, null, null);
            Check(Object.Equals(b.ViewData["HasServerInventoryFilter"], false), "All Inventory must not retain a server-subset reset flag.");
            Count(a, 11, "Other visitor All must not reset an open result page");
            a.Suvs(null);
            Count(a, 128, "SUV category"); Count(b, 200, "Category isolation");
            Check(cached.Suvs == null && cached.Title == "Cached", "Category/title metadata must remain request-local.");
            Count(Create(), 200, "Fresh request after category");

            var firstUrl = SearchUrl(Create().ApplyTerm("  eleven  "));
            var secondUrl = SearchUrl(Create().ApplyTerm("OTHER"));
            var first = Follow(firstUrl); var second = Follow(secondUrl);
            Count(first, 11, "Term POST to GET"); Count(second, 189, "Second tab term");
            Check(Object.Equals(first.ViewData["HasServerInventoryFilter"], true), "Search pages must expose the server-subset reset flag.");
            Count(Follow(firstUrl), 11, "Refreshing first tab after second search");
            var menu = Create(); menu.Index(); Count(menu, 200, "Plain menu after searches");
            Check(Object.Equals(menu.ViewData["HasServerInventoryFilter"], false), "Plain inventory must not retain the server-subset reset flag.");
            Count(Follow(firstUrl), 11, "Menu must not discard another tab's result URL");
            Count(Follow(SearchUrl((JsonResult)Create().Reset())), 200, "Existing reset action restores all");
            Count(Follow(firstUrl), 11, "Reset must not discard another tab's result URL");
            Count(Follow(SearchUrl(Create().ApplyTerm("NO-SUCH-VEHICLE"))), 0, "Zero matches must stay empty");
            Count(Follow(SearchUrl(Create().ApplyTerm("  "))), 200, "Blank term restores all");
            Count(Follow(SearchUrl(Create().ApplyMakes(new[] { "ELEVEN" }))), 11, "Make POST to GET");
            Count(Follow(SearchUrl(Create().ApplyMakes(new[] { "MISSING" }))), 0, "Unknown make stays empty");
            Count(Follow(SearchUrl(Create().ApplyMakes(new string[0]))), 200, "Empty make selection restores all");
            var filter = new Filters { Makes = new[] { "ELEVEN" }, Transmissions = new[] { "Automatic" },
                MinPrice = 1, MaxPrice = 20000, MinMilege = 1, MaxMilege = 100000 };
            Count(Follow(SearchUrl(Create().ApplyFilter(filter))), 11, "Full filter POST to GET");
            filter.MaxPrice = 10000;
            Count(Follow(SearchUrl(Create().ApplyFilter(filter))), 0, "Full filter with zero matches");

            // A short result URL must retain all matches and their search order, even over 250 rows.
            var standardInventory = cached;
            var large = Enumerable.Range(1, 600).Select(i => new GTX.Models.GTX {
                Stock = "LARGE" + i.ToString("D4"), Make = i % 2 == 0 ? "ALPHA" : "BETA",
                Model = i % 3 == 0 ? "First" : "Second", VehicleType = "SUV", InternetPrice = 15000,
                Mileage = 40000, Transmission = "A", Cylinders = 4
            }).Reverse().ToArray();
            try {
                cached = new Inventory { All = large, Vehicles = large };
                var termUrl = SearchUrl(Create().ApplyTerm("LARGE"));
                Check(termUrl.Length < 120, "Large searches must use a short URL.");
                var largeTerm = Follow(termUrl);
                Count(largeTerm, 600, "Large term search must not truncate");
                Check(largeTerm.Model.Inventory.Vehicles.Select(v => v.Stock).SequenceEqual(large.OrderBy(v => v.Make).Select(v => v.Stock)), "Term searches must preserve their Make ordering.");
                var makes = Follow(SearchUrl(Create().ApplyMakes(new[] { "ALPHA", "BETA" })));
                Check(makes.Model.Inventory.Vehicles.Select(v => v.Stock).SequenceEqual(large.OrderBy(v => v.Make).ThenBy(v => v.Model).Select(v => v.Stock)), "Make searches must preserve Make/Model ordering.");
                var ranges = new Filters { MinPrice = 1, MaxPrice = 20000, MinMilege = 1, MaxMilege = 100000 };
                var filtered = Follow(SearchUrl(Create().ApplyFilter(ranges)));
                Check(filtered.Model.Inventory.Vehicles.Select(v => v.Stock).SequenceEqual(large.OrderBy(v => v.Make).ThenBy(v => v.Model).Select(v => v.Stock)), "Full filter searches must preserve Make/Model ordering.");
                cached.All = large.Skip(1).ToArray();
                Count(Follow(termUrl), 599, "Search snapshots must omit vehicles no longer in current inventory");
            }
            finally { cached = standardInventory; }

            // Stale presentation fields must never become the starting point for a new request.
            cached.Vehicles = all.Take(11).ToArray();
            Count(Create(), 200, "Request starts from All even if cached Vehicles is stale");
            cached.Vehicles = all;

            var token = InventorySearchResults.Store(new[] { " STOCK001 ", "stock001", "STOCK002", "", null });
            Remember(token);
            var found = InventorySearchResults.Find(token);
            Check(found.Length == 2 && found[0] == "STOCK001", "Search stock snapshots normalize and deduplicate.");
            found[0] = "MODIFIED";
            Check(InventorySearchResults.Find(token)[0] == "STOCK001", "Reading a result must not expose the saved array.");
            var emptyToken = InventorySearchResults.Store(new string[0]); Remember(emptyToken);
            Check(InventorySearchResults.Find(emptyToken).Length == 0, "An empty result must have a valid snapshot.");
            var emptyPage = Create(); emptyPage.Index(emptyToken);
            Check(Object.Equals(emptyPage.ViewData["HasServerInventoryFilter"], true), "Zero-result pages must still expose the server-subset reset flag.");
            foreach (var invalid in new[] { "", "invalid", Guid.NewGuid().ToString("N") }) {
                var expired = Create();
                var result = expired.Index(invalid);
                Check(expired.Response.StatusCode == 410 && result is ContentResult, "Invalid result URLs must fail closed with 410.");
            }
            Check(Follow("/Inventory/Index?search=").Response.StatusCode == 410, "MVC binding of an empty search marker must fail closed.");
            Check(Follow("/Inventory/Index?search=%20%20").Response.StatusCode == 410, "MVC binding of a whitespace search marker must fail closed.");
            var expiredUrl = SearchUrl(Create().ApplyTerm("ELEVEN"));
            var expiredToken = HttpUtility.ParseQueryString(new Uri("https://inventory.test" + expiredUrl).Query)["search"];
            MemoryCache.Default.Remove("GTX:InventorySearchResults:" + expiredToken);
            var expiredSearch = Create(); expiredSearch.Index(expiredToken);
            Check(expiredSearch.Response.StatusCode == 410, "Evicted search state must not silently show all inventory.");

            var stockList = String.Join(",", all.Take(128).Select(v => v.Stock));
            var stocks = Create(); stocks.All(null, null, null, stockList);
            Count(stocks, 128, "Existing stock-list filter");
            Check(Object.Equals(stocks.ViewData["HasServerInventoryFilter"], true), "Stock filters must expose the server-subset reset flag.");
            var chatToken = ChatInventoryResults.Store(all.Take(11).Select(v => v.Stock));
            try {
                var chat = Create(); chat.All(null, null, null, null, chatToken);
                Count(chat, 11, "Existing chatbot result token");
                Check(Object.Equals(chat.ViewData["HasServerInventoryFilter"], true), "Chatbot filters must expose the server-subset reset flag.");
                Count(Create(), 200, "Chatbot result isolation");
            }
            finally { MemoryCache.Default.Remove("GTX:ChatInventoryResults:" + chatToken); }

            // Concurrent request wrappers share vehicle data, but not their current result selection.
            var requests = Enumerable.Range(0, 24).Select(i => Create()).ToArray();
            Parallel.For(0, requests.Length, i => {
                if (i % 2 == 0) requests[i].All("ELEVEN", null, null, null);
                else requests[i].Suvs(null);
            });
            for (var i = 0; i < requests.Length; i++) Count(requests[i], i % 2 == 0 ? 11 : 128, "Concurrent request " + i);
            Check(cached.Vehicles.Length == 200 && cached.All.Length == 200 && cached.Title == "Cached", "No action may alter the cached selection.");
            Console.WriteLine("PASS: " + assertions + " inventory isolation assertions; search POST/GET, zero matches, tabs/refresh, expired tokens, categories and chatbot filters.");
        }
        finally {
            AppCache.Remove(cacheKey);
            AppCache.Remove(cacheKey + ":lock");
            foreach (var token in searchTokens) MemoryCache.Default.Remove("GTX:InventorySearchResults:" + token);
        }
    }

    private static InventoryController Create(Inventory source = null, string path = "/Inventory/Index")
    {
        var context = new TestContext(path);
        var routeData = new RouteData();
        routeData.Values["controller"] = "Inventory";
        routeData.Values["action"] = path.Split('?')[0].EndsWith("/All", StringComparison.OrdinalIgnoreCase) ? "All" : "Index";
        var request = new RequestContext(context, routeData);
        var routes = new RouteCollection();
        routes.MapRoute("Default", "{controller}/{action}/{id}", new { controller = "Home", action = "Index", id = UrlParameter.Optional });
        var controller = new InventoryController(new TestSession(), null, null, new TestLog(), null) {
            Model = new BaseModel { Inventory = (source ?? cached).CreateForRequest(),
                Categories = cached.All.GroupBy(v => v.VehicleType).ToDictionary(g => g.Key, g => g.ToArray()) }
        };
        controller.ControllerContext = new ControllerContext(request, controller);
        controller.Url = new UrlHelper(request, routes);
        return controller;
    }

    private static string SearchUrl(JsonResult response)
    {
        var property = response.Data.GetType().GetProperty("redirectUrl");
        Check(property != null, "Search response must contain redirectUrl.");
        var url = (string)property.GetValue(response.Data, null);
        Check(!String.IsNullOrEmpty(url), "Search redirects must provide a URL.");
        var uri = new Uri("https://inventory.test" + url);
        Check(new[] { "/Inventory", "/Inventory/Index", "/Inventory/All" }.Contains(uri.AbsolutePath, StringComparer.OrdinalIgnoreCase), "Search redirects must stay within inventory: " + url);
        var token = HttpUtility.ParseQueryString(uri.Query)["search"];
        if (token != null) Remember(token);
        return url;
    }

    private static InventoryController Follow(string url)
    {
        var uri = new Uri("https://inventory.test" + url);
        var controller = Create(path: url);
        if (uri.AbsolutePath.EndsWith("/All", StringComparison.OrdinalIgnoreCase)) controller.All(null, null, null, null);
        else {
            var query = HttpUtility.ParseQueryString(uri.Query);
            var binding = new ModelBindingContext { ModelName = "search", ModelMetadata = ModelMetadataProviders.Current.GetMetadataForType(null, typeof(string)),
                ValueProvider = new NameValueCollectionValueProvider(query, CultureInfo.InvariantCulture) };
            var search = (string)new DefaultModelBinder().BindModel(controller.ControllerContext, binding);
            controller.Index(search);
        }
        return controller;
    }

    private static void Remember(string token) { if (token != null) searchTokens.Add(token); }
    private static void Count(InventoryController controller, int expected, string name)
    {
        Check(controller.Model.Inventory.Vehicles.Length == expected, name + ": expected " + expected + ", got " + controller.Model.Inventory.Vehicles.Length + ".");
    }
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); assertions++; }

    private sealed class TestContext : HttpContextBase
    {
        private readonly TestRequest request;
        private readonly TestResponse response = new TestResponse();
        private readonly IDictionary items = new Hashtable();
        public TestContext(string path) { request = new TestRequest(path); }
        public override HttpRequestBase Request { get { return request; } }
        public override HttpResponseBase Response { get { return response; } }
        public override IDictionary Items { get { return items; } }
        public override object GetService(Type serviceType) { return null; }
    }
    private sealed class TestRequest : HttpRequestBase
    {
        private readonly Uri uri;
        public TestRequest(string path) { uri = new Uri("https://inventory.test" + path); }
        public override string ApplicationPath { get { return "/"; } }
        public override string AppRelativeCurrentExecutionFilePath { get { return "~" + uri.AbsolutePath; } }
        public override string CurrentExecutionFilePath { get { return uri.AbsolutePath; } }
        public override string Path { get { return uri.AbsolutePath; } }
        public override string PathInfo { get { return ""; } }
        public override string HttpMethod { get { return "GET"; } }
        public override Uri Url { get { return uri; } }
        public override NameValueCollection QueryString { get { return HttpUtility.ParseQueryString(uri.Query); } }
        public override NameValueCollection ServerVariables { get { return new NameValueCollection(); } }
    }
    private sealed class TestResponse : HttpResponseBase
    {
        private int statusCode = 200;
        public override string ApplyAppPathModifier(string path) { return path; }
        public override int StatusCode { get { return statusCode; } set { statusCode = value; } }
        public override bool TrySkipIisCustomErrors { get; set; }
    }
    private sealed class TestSession : ISessionData
    {
        public CommonUnit.Environment Environment { get; set; }
        public CommonUnit.Responsibility Responsibility { get; set; }
        public bool? IsMajordome { get; set; }
        public Log LogHeader { get; set; }
        public T GetSession<T>(string key) { return default(T); }
        public T GetSession<T>(string key, T defaultValue) { return defaultValue; }
        public void SetSession(string key, object value) { }
    }
    private sealed class TestLog : ILogService
    {
        public void Log(Log header, string action) { }
        public void Log(Log header, CommonUnit.LogType logType) { }
        public void Log(Log header, Exception ex) { }
        public Log[] GetLogs() { return new Log[0]; }
    }
}
