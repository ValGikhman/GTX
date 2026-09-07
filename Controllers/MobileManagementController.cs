using GTX.Helpers;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Services;
using System;
using System.Configuration;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Web;
using System.Web.Caching;
using System.Web.Mvc;

namespace GTX.Controllers
{
    // Native client API, separate from the anonymous public inventory API.
    // Uses the existing configured Owner password and persistent, revocable opaque sessions.
    public sealed class MobileManagementController : Controller {
        private static readonly object LoginLock = new object();
        private readonly IInventoryService inventory;
        private MobileSessionStore Sessions => new MobileSessionStore(Server.MapPath("~/App_Data/MobileSessions"));
        private sealed class AttemptCounter { public int Count; }
        public sealed class LoginRequest { public string Password { get; set; } }
        public MobileManagementController() : this(new InventoryService()) { }
        public MobileManagementController(IInventoryService service) { inventory = service; }
        private static string OwnerPassword => ConfigurationManager.AppSettings.AllKeys
            .Where(k => string.Equals(k, "sitePassword:Owner", StringComparison.OrdinalIgnoreCase))
            .Select(k => ConfigurationManager.AppSettings[k]).FirstOrDefault();
        private static string Hash(string value) {
            using (var sha = SHA256.Create()) return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(value ?? "")));
        }
        private string BearerToken {
            get {
                var header = Request.Headers["Authorization"] ?? "";
                return header.StartsWith("Bearer ", StringComparison.Ordinal) && header.Length < 512
                    ? header.Substring(7) : null;
            }
        }
        protected override void OnActionExecuting(ActionExecutingContext context) {
            Response.Cache.SetCacheability(HttpCacheability.NoCache);
            Response.Cache.SetNoStore();
            Response.SuppressFormsAuthenticationRedirect = true;
            if (!string.Equals(context.ActionDescriptor.ActionName, "Login", StringComparison.OrdinalIgnoreCase)) {
                if (!Sessions.IsValid(BearerToken, OwnerPassword)) {
                    context.Result = Payload(new { message = "Sign in with your GTX Owner account." }, 401);
                }
            }
            base.OnActionExecuting(context);
        }
        private ContentResult Payload(object value, int status = 200) {
            Response.StatusCode = status;
            Response.TrySkipIisCustomErrors = true;
            return Content(JsonConvert.SerializeObject(value, new JsonSerializerSettings {
                ContractResolver = new CamelCasePropertyNamesContractResolver(),
                NullValueHandling = NullValueHandling.Ignore
            }), "application/json", Encoding.UTF8);
        }
        [HttpPost]
        public ActionResult Login(LoginRequest request) {
            // Limit attempts per remote client; also applies through a local dev gateway.
            var key = "mobile-login:" + Hash(Request.UserHostAddress);
            lock (LoginLock) {
                var attempts = HttpRuntime.Cache[key] as AttemptCounter;
                if (attempts == null) {
                    attempts = new AttemptCounter();
                    HttpRuntime.Cache.Insert(key, attempts, null, DateTime.UtcNow.AddMinutes(5), Cache.NoSlidingExpiration);
                }
                if (attempts.Count >= 10) return Payload(new { message = "Too many attempts. Try again in five minutes." }, 429);
                attempts.Count++;
            }
            var expected = OwnerPassword;
            var supplied = request?.Password?.Trim();
            if (string.IsNullOrWhiteSpace(expected) || supplied == null || supplied.Length > 512 || Hash(expected) != Hash(supplied))
                return Payload(new { message = "Invalid Owner password." }, 401);
            var token = Sessions.Create(expected);
            HttpRuntime.Cache.Remove(key);
            return Payload(new { token });
        }
        [HttpPost]
        public ActionResult Logout() {
            Sessions.Revoke(BearerToken);
            return Payload(new { success = true });
        }
        [HttpGet]
        public ActionResult Inventory(int days = 7) {
            if (days != 7 && days != 30 && days != 60) return Payload(new { message = "Choose 7, 30, or 60 days." }, 400);
            try {
                // Same service as MVC, deliberately bypassing the MVC five-minute cache.
                var dashboard = inventory.GetInventoryDashboard(days);
                var current = inventory.GetInventory(true, false);
                var byStock = current.vehicles.GroupBy(v => (v.Stock ?? "").Trim(), StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
                return Payload(new {
                    fetchedAt = DateTime.UtcNow, inventoryDate = current.InventoryDate,
                    environment = CommonUnit.GetConfiguredResponsibility() + "/" + CommonUnit.GetConfiguredEnvironment(),
                    dashboard.StatusCounts, dashboard.LocationCounts, dashboard.StatusTrend,
                    dashboard.PeriodStartUtc, dashboard.PeriodEndUtc,
                    vehicles = dashboard.Vehicles.Select(v => {
                        byStock.TryGetValue((v.Stock ?? "").Trim(), out var basic);
                        return new {
                            stock = (v.Stock ?? "").Trim(), name = (v.Year + " " + v.Make + " " + v.Model).Trim(),
                            vin = v.VIN, status = v.StatusText, statuses = v.Statuses, isCurrent = v.IsCurrent,
                            location = (v.LocationCode ?? "N/A").Trim(), age = v.DaysInInventory,
                            mileage = basic == null ? (int?)null : basic.Mileage,
                            price = basic == null ? (int?)null : basic.InternetPrice,
                            thumbnailUrl = string.IsNullOrWhiteSpace(v.Image) ? null : InventoryImageUrl.Build(v.Image, v.Stock, InventoryImageVariant.Grid),
                            statusEvents = v.StatusEvents
                        };
                    }).ToArray()
                });
            } catch (Exception ex) {
                System.Diagnostics.Trace.TraceError("Mobile inventory: {0}", ex);
                return Payload(new { message = "Inventory could not be loaded. Try again." }, 503);
            }
        }
        [HttpGet]
        public ActionResult Details(string stock) {
            stock = (stock ?? "").Trim();
            if (stock.Length == 0 || stock.Length > 32 || stock.Any(c => !char.IsLetterOrDigit(c) && c != '-'))
                return Payload(new { message = "Invalid stock number." }, 400);
            try {
                var current = inventory.GetInventory(true, false).vehicles.FirstOrDefault(v => string.Equals(v.Stock?.Trim(), stock, StringComparison.OrdinalIgnoreCase))
                    ?? inventory.GetInventoryVehicleSnapshot(stock);
                if (current == null) return Payload(new { message = "Vehicle no longer available." }, 404);
                var rawDataOne = inventory.GetDataOneDetails(stock);
                var decoded = string.IsNullOrWhiteSpace(rawDataOne) ? null : Models.GTX.SetDecodedData(rawDataOne);
                return Payload(new {
                    stock, fetchedAt = DateTime.UtcNow,
                    images = inventory.GetImages(stock).Select(i => InventoryImageUrl.Build(i.Source, stock, InventoryImageVariant.Detail)).ToArray(),
                    profile = new {
                        current.VIN, current.Engine, transmission = Models.GTX.WordIt(current.Transmission),
                        current.Body, fuel = current.FuelType, current.Color, current.Color2,
                        current.DriveTrain, current.Mileage, current.InternetPrice, current.RetailPrice,
                        documentaryFee = current.InternetPrice > 0 ? Constants.DOCUMENTARY_FEE : 0,
                        transparentPrice = current.InternetPrice + (current.InternetPrice > 0 ? Constants.DOCUMENTARY_FEE : 0)
                    },
                    dataOne = decoded,
                    dataOneState = decoded != null ? "available" : string.IsNullOrWhiteSpace(rawDataOne) ? "missing" : "unavailable",
                    history = inventory.GetInventoryDashboardVehicleHistory(stock)
                });
            } catch (Exception ex) {
                System.Diagnostics.Trace.TraceError("Mobile details: {0}", ex);
                return Payload(new { message = "Vehicle details could not be loaded. Try again." }, 503);
            }
        }
    }
}
