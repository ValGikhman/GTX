using Common;
using GTX.Helpers;
using GTX.Models;
using Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Web;
using System.Web.Hosting;
using System.Web.Http;

namespace GTX.Controllers {
    [AllowAnonymous]
    [RoutePrefix("api/v1")]
    public sealed class ApiController : System.Web.Http.ApiController {
        private const int MaximumPageSize = 100;
        private const int MaximumMetaImages = 20;
        private readonly IInventoryService _inventoryService;

        public ApiController() : this(new InventoryService()) { }

        public ApiController(IInventoryService inventoryService) {
            _inventoryService = inventoryService ?? throw new ArgumentNullException(nameof(inventoryService));
        }

        [HttpGet]
        [Route("health")]
        public IHttpActionResult Health() {
            return Ok(new {
                status = "ok",
                apiVersion = "v1",
                serverTimeUtc = DateTime.UtcNow
            });
        }

        [HttpGet]
        [Route("showmehow")]
        public IHttpActionResult ShowMeHow() {
            var pagePath = HostingEnvironment.MapPath("~/Content/api-showmehow.html");
            if (string.IsNullOrWhiteSpace(pagePath) || !File.Exists(pagePath)) {
                return Content(
                    HttpStatusCode.InternalServerError,
                    "API documentation is temporarily unavailable.");
            }

            var response = new HttpResponseMessage(HttpStatusCode.OK) {
                Content = new StringContent(File.ReadAllText(pagePath), Encoding.UTF8, "text/html")
            };
            return ResponseMessage(response);
        }

        [HttpGet]
        [Route("config")]
        public IHttpActionResult Config() {
            var hours = Utility.XMLHelpers.XmlRepository.GetOpenHours() ?? Array.Empty<OpenHours>();

            return Ok(new MobileAppConfigResponse {
                ApiVersion = "v1",
                DealerName = "GTX Auto Group",
                Phone = "+15134892886",
                Address = "9516 Princeton Glendale Rd, West Chester, OH 45011",
                WebsiteBaseUrl = GetBaseUrl(),
                DirectionsUrl = "https://www.google.com/maps/search/?api=1&query=9516+Princeton+Glendale+Rd+West+Chester+OH+45011",
                CreditApplicationUrl = BuildUrl("Home/Application"),
                DocumentaryFee = Constants.DOCUMENTARY_FEE,
                Hours = hours.Select(item => new MobileStoreHoursDto {
                    Day = item.Day,
                    Description = item.Description
                }).ToArray()
            });
        }

        [HttpGet]
        [Route("filters")]
        public IHttpActionResult Filters() {
            try {
                var inventory = _inventoryService.GetInventory();
                var vehicles = inventory.vehicles ?? Array.Empty<GTXDTO>();

                return Ok(new MobileInventoryFiltersResponse {
                    Published = inventory.InventoryDate,
                    TotalCount = vehicles.Length,
                    Makes = DistinctValues(vehicles.Select(v => v.Make)),
                    Models = DistinctValues(vehicles.Select(v => v.Model)),
                    VehicleTypes = DistinctValues(vehicles.Select(v => v.VehicleType)),
                    BodyTypes = DistinctValues(vehicles.Select(v => v.Body)),
                    FuelTypes = DistinctValues(vehicles.Select(v => v.FuelType)),
                    Drivetrains = DistinctValues(vehicles.Select(v => v.DriveTrain)),
                    Transmissions = DistinctValues(vehicles.Select(v => Models.GTX.WordIt(v.Transmission))),
                    MinYear = MinOrZero(vehicles.Select(v => v.Year)),
                    MaxYear = MaxOrZero(vehicles.Select(v => v.Year)),
                    MinMileage = MinOrZero(vehicles.Select(v => v.Mileage)),
                    MaxMileage = MaxOrZero(vehicles.Select(v => v.Mileage)),
                    MinPrice = MinOrZero(vehicles.Select(v => v.InternetPrice)),
                    MaxPrice = MaxOrZero(vehicles.Select(v => v.InternetPrice))
                });
            }
            catch (Exception ex) {
                return InventoryError(ex);
            }
        }

        [HttpGet]
        [Route("inventory")]
        public IHttpActionResult Inventory([FromUri] MobileInventoryQuery query) {
            query = query ?? new MobileInventoryQuery();
            var validationError = ValidateQuery(query);
            if (validationError != null) {
                return BadRequest(validationError);
            }

            try {
                var inventory = _inventoryService.GetInventory();
                var filtered = ApplyQuery(inventory.vehicles ?? Array.Empty<GTXDTO>(), query);
                var totalCount = filtered.Count();
                var pageVehicles = filtered
                    .Skip((query.Page - 1) * query.PageSize)
                    .Take(query.PageSize)
                    .ToArray();
                var counters = GetDetailsCountersSafely();

                return Ok(new MobileInventoryListResponse {
                    Published = inventory.InventoryDate,
                    Page = query.Page,
                    PageSize = query.PageSize,
                    TotalCount = totalCount,
                    TotalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)query.PageSize),
                    DocumentaryFee = Constants.DOCUMENTARY_FEE,
                    Vehicles = pageVehicles
                        .Select(vehicle => ToMobileVehicle(vehicle, includeDetails: false, counters: counters))
                        .ToArray()
                });
            }
            catch (Exception ex) {
                return InventoryError(ex);
            }
        }

        [HttpGet]
        [Route("inventory/{stock}")]
        public IHttpActionResult Vehicle(string stock) {
            var normalizedStock = (stock ?? string.Empty).Trim().ToUpperInvariant();
            if (normalizedStock.Length == 0 || normalizedStock.Length > 10 ||
                normalizedStock.Any(ch => !char.IsLetterOrDigit(ch) && ch != '-')) {
                return BadRequest("A valid stock number is required.");
            }

            try {
                var inventory = _inventoryService.GetInventory();
                var vehicle = (inventory.vehicles ?? Array.Empty<GTXDTO>())
                    .FirstOrDefault(item => string.Equals(
                        item.Stock?.Trim(),
                        normalizedStock,
                        StringComparison.OrdinalIgnoreCase));

                if (vehicle == null) {
                    return NotFound();
                }

                var counters = GetDetailsCountersSafely();
                try {
                    counters[normalizedStock] = _inventoryService.IncrementDetailsCounter(normalizedStock);
                }
                catch (Exception counterError) {
                    Trace.TraceError(
                        "Unable to increment mobile details counter for {0}: {1}",
                        normalizedStock,
                        counterError);
                }

                return Ok(ToMobileVehicle(vehicle, includeDetails: true, counters: counters));
            }
            catch (Exception ex) {
                return InventoryError(ex);
            }
        }

        [HttpGet]
        [Route("meta-vehicles")]
        public IHttpActionResult MetaVehicles() {
            try {
                var inventory = _inventoryService.GetInventory();
                var csv = new StringBuilder();
                var headers = new List<string> {
                    "vehicle_id",
                    "title",
                    "description",
                    "url",
                    "make",
                    "model",
                    "year",
                    "mileage.value",
                    "mileage.unit"
                };

                for (var imageIndex = 0; imageIndex < MaximumMetaImages; imageIndex++) {
                    headers.Add("image[" + imageIndex + "].url");
                }

                headers.AddRange(new[] {
                    "price",
                    "state_of_vehicle",
                    "vin",
                    "transmission",
                    "body_style",
                    "drivetrain"
                });
                csv.AppendLine(string.Join(",", headers));

                foreach (var vehicle in inventory.vehicles ?? Array.Empty<GTXDTO>()) {
                    var stock = (vehicle.Stock ?? string.Empty).Trim();
                    var vin = (vehicle.VIN ?? string.Empty).Trim();
                    var imageUrls = GetMetaImageUrls(stock);

                    // Meta requires a stable ID, price, VIN, landing page, and vehicle image.
                    if (stock.Length == 0 || vin.Length == 0 || vehicle.InternetPrice <= 0 || imageUrls.Length == 0) {
                        continue;
                    }

                    var title = string.Join(" ", new[] {
                        vehicle.Year.ToString(),
                        vehicle.Make,
                        vehicle.Model,
                        vehicle.VehicleStyle
                    }.Where(value => !string.IsNullOrWhiteSpace(value)));
                    var values = new List<object> {
                        stock,
                        title,
                        title,
                        BuildUrl("Inventory/Details?stock=" + HttpUtility.UrlEncode(stock)),
                        vehicle.Make,
                        vehicle.Model,
                        vehicle.Year,
                        vehicle.Mileage,
                        "MI"
                    };

                    for (var imageIndex = 0; imageIndex < MaximumMetaImages; imageIndex++) {
                        values.Add(imageIndex < imageUrls.Length ? imageUrls[imageIndex] : string.Empty);
                    }

                    values.Add(vehicle.InternetPrice.ToString("0.00") + " USD");
                    values.Add("USED");
                    values.Add(vin);
                    values.Add(BuildTransmission(vehicle));
                    values.Add(vehicle.Body);
                    values.Add(vehicle.DriveTrain);
                    csv.AppendLine(string.Join(",", values.Select(Csv)));
                }

                var response = new HttpResponseMessage(HttpStatusCode.OK) {
                    Content = new StringContent(csv.ToString(), Encoding.UTF8, "text/csv")
                };
                response.Content.Headers.ContentDisposition = new ContentDispositionHeaderValue("inline") {
                    FileName = "meta-vehicles.csv"
                };
                return ResponseMessage(response);
            }
            catch (Exception ex) {
                return InventoryError(ex);
            }
        }

        private IEnumerable<GTXDTO> ApplyQuery(IEnumerable<GTXDTO> source, MobileInventoryQuery query) {
            var vehicles = source ?? Enumerable.Empty<GTXDTO>();

            if (!string.IsNullOrWhiteSpace(query.Search)) {
                var term = query.Search.Trim();
                vehicles = vehicles.Where(v =>
                    Contains(v.Stock, term) || Contains(v.VIN, term) ||
                    Contains(v.Make, term) || Contains(v.Model, term) ||
                    Contains(v.VehicleStyle, term) || v.Year.ToString().Contains(term));
            }

            vehicles = FilterText(vehicles, query.Type, v => v.VehicleType);
            vehicles = FilterText(vehicles, query.Make, v => v.Make);
            vehicles = FilterText(vehicles, query.Model, v => v.Model);

            if (query.MinYear.HasValue) vehicles = vehicles.Where(v => v.Year >= query.MinYear.Value);
            if (query.MaxYear.HasValue) vehicles = vehicles.Where(v => v.Year <= query.MaxYear.Value);
            if (query.MinMileage.HasValue) vehicles = vehicles.Where(v => v.Mileage >= query.MinMileage.Value);
            if (query.MaxMileage.HasValue) vehicles = vehicles.Where(v => v.Mileage <= query.MaxMileage.Value);
            if (query.MinPrice.HasValue) vehicles = vehicles.Where(v => v.InternetPrice >= query.MinPrice.Value);
            if (query.MaxPrice.HasValue) vehicles = vehicles.Where(v => v.InternetPrice <= query.MaxPrice.Value);

            switch ((query.Sort ?? "featured").Trim().ToLowerInvariant()) {
                case "price-low":
                    return vehicles.OrderBy(v => v.InternetPrice).ThenByDescending(v => v.Year);
                case "price-high":
                    return vehicles.OrderByDescending(v => v.InternetPrice).ThenByDescending(v => v.Year);
                case "mileage":
                    return vehicles.OrderBy(v => v.Mileage).ThenByDescending(v => v.Year);
                case "year":
                    return vehicles.OrderByDescending(v => v.Year).ThenBy(v => v.Make).ThenBy(v => v.Model);
                case "make":
                    return vehicles.OrderBy(v => v.Make).ThenBy(v => v.Model).ThenByDescending(v => v.Year);
                default:
                    return vehicles.OrderByDescending(v => v.DateCreated).ThenBy(v => v.Make).ThenBy(v => v.Model);
            }
        }

        private MobileVehicleDto ToMobileVehicle(
            GTXDTO vehicle,
            bool includeDetails,
            IDictionary<string, long> counters) {

            var normalizedStock = (vehicle.Stock ?? string.Empty).Trim();
            var imageUrls = GetImageUrls(normalizedStock);
            long detailsViews;
            counters.TryGetValue(normalizedStock, out detailsViews);
            var documentaryFee = vehicle.InternetPrice > 0 ? Constants.DOCUMENTARY_FEE : 0;

            return new MobileVehicleDto {
                Stock = normalizedStock,
                Vin = vehicle.VIN,
                Year = vehicle.Year,
                Make = vehicle.Make,
                Model = vehicle.Model,
                Trim = vehicle.VehicleStyle,
                Type = vehicle.VehicleType,
                Body = vehicle.Body,
                Mileage = vehicle.Mileage,
                Cylinders = vehicle.Cylinders,
                RetailPrice = vehicle.RetailPrice,
                InternetPrice = vehicle.InternetPrice,
                DocumentaryFee = documentaryFee,
                TransparentPrice = vehicle.InternetPrice + documentaryFee,
                ExteriorColor = vehicle.Color,
                InteriorColor = vehicle.Color2,
                Drivetrain = vehicle.DriveTrain,
                Engine = vehicle.Engine,
                Transmission = BuildTransmission(vehicle),
                Fuel = vehicle.FuelType,
                LocationCode = vehicle.LocationCode,
                PrimaryImageUrl = imageUrls.FirstOrDefault(),
                ImageUrls = includeDetails ? imageUrls : imageUrls.Take(1).ToArray(),
                Features = SplitFeatures(vehicle.Features),
                HasStory = vehicle.Story != null && !string.IsNullOrWhiteSpace(vehicle.Story.HtmlContent),
                StoryTitle = includeDetails ? vehicle.Story?.Title : null,
                StoryHtml = includeDetails ? vehicle.Story?.HtmlContent : null,
                DetailsViews = detailsViews,
                WebsiteUrl = BuildUrl("Inventory/Details?stock=" + HttpUtility.UrlEncode(normalizedStock)),
                CarfaxUrl = "https://www.carfax.com/VehicleHistory/p/Report.cfx?partner=DVW_1&vin=" +
                    HttpUtility.UrlEncode(vehicle.VIN ?? string.Empty)
            };
        }

        private string[] GetImageUrls(string stock) {
            try {
                var images = _inventoryService.GetImages(stock) ?? Array.Empty<Image>();
                var urls = images
                    .Select(image => BuildPictureUrl(stock, image.Source))
                    .Where(url => !string.IsNullOrWhiteSpace(url))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                return urls.Length > 0 ? urls : new[] { BuildUrl("Pictures/no-image-1.jpg") };
            }
            catch (Exception ex) {
                Trace.TraceError("Unable to load mobile inventory images for {0}: {1}", stock, ex);
                return new[] { BuildUrl("Pictures/no-image-1.jpg") };
            }
        }

        private string[] GetMetaImageUrls(string stock) {
            try {
                return (_inventoryService.GetImages(stock) ?? Array.Empty<Image>())
                    .Select(image => BuildPictureUrl(stock, image.Source))
                    .Where(url => !string.IsNullOrWhiteSpace(url))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Take(MaximumMetaImages)
                    .ToArray();
            }
            catch (Exception ex) {
                Trace.TraceError("Unable to load Meta inventory images for {0}: {1}", stock, ex);
                return Array.Empty<string>();
            }
        }

        private string BuildPictureUrl(string stock, string source) {
            var value = (source ?? string.Empty).Trim().Replace('\\', '/');
            Uri absolute;
            if (Uri.TryCreate(value, UriKind.Absolute, out absolute)) {
                return absolute.ToString();
            }

            value = value.TrimStart('/');
            foreach (var prefix in new[] { "Pictures/", "SiteImages/Inventory/", "Images/" }) {
                if (value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) {
                    value = value.Substring(prefix.Length);
                    break;
                }
            }

            var normalizedStock = (stock ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(normalizedStock) &&
                !value.StartsWith(normalizedStock + "/", StringComparison.OrdinalIgnoreCase)) {
                value = normalizedStock + "/" + value;
            }

            var encodedPath = string.Join("/", value
                .Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(Uri.EscapeDataString));
            if (string.IsNullOrWhiteSpace(encodedPath)) {
                return null;
            }

            return InventoryImageSettings.CloudflareEnabled
                ? InventoryImageSettings.BaseUrl + "/" + encodedPath
                : BuildUrl("Pictures/" + encodedPath);
        }

        private string BuildUrl(string relativePath) {
            return new Uri(
                new Uri(GetBaseUrl() + "/"),
                (relativePath ?? string.Empty).TrimStart('/'))
                .ToString();
        }

        private string GetBaseUrl() {
            return Request?.RequestUri?.GetLeftPart(UriPartial.Authority)?.TrimEnd('/') ??
                "https://usedcarscincinnati.com";
        }

        private Dictionary<string, long> GetDetailsCountersSafely() {
            try {
                var result = _inventoryService.GetDetailsCounters();
                return result == null
                    ? new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase)
                    : new Dictionary<string, long>(result, StringComparer.OrdinalIgnoreCase);
            }
            catch (Exception ex) {
                Trace.TraceError("Unable to load mobile inventory details counters: {0}", ex);
                return new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
            }
        }

        private IHttpActionResult InventoryError(Exception ex) {
            Trace.TraceError("Public inventory API failure: {0}", ex);
            return Content(
                HttpStatusCode.InternalServerError,
                new { message = "Inventory is temporarily unavailable. Please try again." });
        }

        private static string ValidateQuery(MobileInventoryQuery query) {
            if (query.Page < 1) return "Page must be at least 1.";
            if (query.PageSize < 1 || query.PageSize > MaximumPageSize) {
                return "Page size must be between 1 and 100.";
            }
            if (query.MinYear.HasValue && query.MaxYear.HasValue && query.MinYear > query.MaxYear) {
                return "Minimum year cannot exceed maximum year.";
            }
            if (query.MinMileage.HasValue && query.MaxMileage.HasValue && query.MinMileage > query.MaxMileage) {
                return "Minimum mileage cannot exceed maximum mileage.";
            }
            if (query.MinPrice.HasValue && query.MaxPrice.HasValue && query.MinPrice > query.MaxPrice) {
                return "Minimum price cannot exceed maximum price.";
            }

            var sort = (query.Sort ?? "featured").Trim().ToLowerInvariant();
            var allowedSorts = new[] { "featured", "price-low", "price-high", "mileage", "year", "make" };
            return allowedSorts.Contains(sort)
                ? null
                : "Sort must be featured, price-low, price-high, mileage, year, or make.";
        }

        private static IEnumerable<GTXDTO> FilterText(
            IEnumerable<GTXDTO> vehicles,
            string value,
            Func<GTXDTO, string> selector) {

            if (string.IsNullOrWhiteSpace(value)) return vehicles;
            var expected = value.Trim();
            return vehicles.Where(vehicle => string.Equals(
                selector(vehicle)?.Trim(),
                expected,
                StringComparison.OrdinalIgnoreCase));
        }

        private static bool Contains(string value, string term) {
            return !string.IsNullOrWhiteSpace(value) &&
                value.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string BuildTransmission(GTXDTO vehicle) {
            var name = Models.GTX.WordIt(vehicle.Transmission);
            return vehicle.TransmissionSpeed > 0
                ? vehicle.TransmissionSpeed + "-Speed " + name
                : name;
        }

        private static string[] SplitFeatures(string features) {
            return (features ?? string.Empty)
                .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(item => item.Trim())
                .Where(item => item.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private static string Csv(object value) {
            var text = Convert.ToString(value) ?? string.Empty;
            text = text
                .Replace("\"", "\"\"")
                .Replace("\r", " ")
                .Replace("\n", " ");
            return "\"" + text + "\"";
        }

        private static string[] DistinctValues(IEnumerable<string> values) {
            return values
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private static int MinOrZero(IEnumerable<int> values) {
            var data = values.ToArray();
            return data.Length == 0 ? 0 : data.Min();
        }

        private static int MaxOrZero(IEnumerable<int> values) {
            var data = values.ToArray();
            return data.Length == 0 ? 0 : data.Max();
        }
    }
}
