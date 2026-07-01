using GTX.Common;
using GTX.Helpers;
using GTX.Models;
using ImageMagick;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using QRCoder;
using Services;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;

namespace GTX.Controllers
{
    [RequireAdminRole]
    public class MajordomeController : BaseController {

        private const int UploadImageMaxWidth = 800;
        private const int UploadImageMaxHeight = 600;
        private const int UploadJpegQuality = 84;
        private const int UploadPngCompressionLevel = 9;
        private const string UploadJpegExtension = ".jpg";
        private const string UploadPngExtension = ".png";
        private const int QrTextMaxLength = 2048;

        private static readonly HashSet<string> UploadImageExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
            ".jpg",
            ".jpeg",
            ".png",
            ".gif",
            ".bmp",
            ".webp",
            ".tif",
            ".tiff",
            ".heic",
            ".heif"
        };

        private sealed class UploadImageResponseDto {
            public Guid Id { get; set; }
            public string Stock { get; set; }
            public string Source { get; set; }
            public string Overlay { get; set; }
            public int? Order { get; set; }
        }

        public sealed class SaveStoryRequest {
            public string Stock { get; set; }

            [AllowHtml]
            public string Story { get; set; }

            public string Title { get; set; }
        }

        public sealed class InventoryVehicleSaveRequest {
            public string OriginalStock { get; set; }
            public string Stock { get; set; }
            public string Year { get; set; }
            public string Make { get; set; }
            public string Model { get; set; }
            public string VIN { get; set; }
            public string Mileage { get; set; }
            public string Cylinders { get; set; }
            public string Weight { get; set; }
            public string Color { get; set; }
            public string Color2 { get; set; }
            public string Features { get; set; }
            public string RetailPrice { get; set; }
            public string InternetPrice { get; set; }
            public string DriveTrain { get; set; }
            public string LocationCode { get; set; }
            public string Body { get; set; }
            public string Engine { get; set; }
            public string Transmission { get; set; }
            public string PurchaseDate { get; set; }
            public string ArrivalDate { get; set; }
            public string FuelType { get; set; }
            public string TransmissionSpeed { get; set; }
            public string VehicleType { get; set; }
            public string VehicleStyle { get; set; }
            public string SetToUpload { get; set; }
        }

        private static readonly Dictionary<string, int> InventoryStringLimits = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) {
            { "Stock", 9 },
            { "Make", 50 },
            { "Model", 50 },
            { "VIN", 17 },
            { "Color", 50 },
            { "Color2", 50 },
            { "DriveTrain", 50 },
            { "LocationCode", 1 },
            { "Body", 50 },
            { "Engine", 50 },
            { "Transmission", 1 },
            { "PurchaseDate", 50 },
            { "ArrivalDate", 50 },
            { "FuelType", 50 },
            { "VehicleType", 50 },
            { "VehicleStyle", 50 },
            { "SetToUpload", 1 }
        };

        private static readonly string[] InventoryIntegerFields = {
            "Year",
            "Mileage",
            "Cylinders",
            "Weight",
            "RetailPrice",
            "InternetPrice",
            "TransmissionSpeed"
        };

        private static readonly object _storyCacheLock = new object();
        private static readonly object _imageCacheLock = new object();
        private static readonly object _dataOneCacheLock = new object();

        public MajordomeController(ISessionData sessionData, IInventoryService inventoryService, IVinDecoderService vinDecoderService
                , ILogService logService, IEmployeesService employeesService
            )
            : base(sessionData, inventoryService, vinDecoderService, logService, employeesService) {
        }

        [HttpGet]
        [Route("Majordome/Inventory")]
        [Route("Majordome/Inventory/{stock}")]
        public ActionResult Inventory(string stock = null) {
            ViewBag.Message = "Inventory management";
            ViewBag.Title = "Inventory management";
            ViewBag.Stock = stock;

            RefreshModel(includeHiddenInventory: true);
            Model.Inventory.Vehicles = Model.Inventory.All;

            return View(Model);
        }

        public ActionResult Logs() {
            ViewBag.Message = "Logs";
            ViewBag.Title = "Logs";
            return View(LogService.GetLogs());
        }

        [HttpPost]
        public async Task<ActionResult> CreateStory(string stock) {
            try {
                var vehicle = Model.Inventory.Vehicles.FirstOrDefault(m => m.Stock == stock);
                if (vehicle == null) {
                    return Json(new { success = false, message = "Vehicle not found." });
                }

                var story = await GetChatGptResponse(GetPrompt(vehicle));
                var response = SplitResponse(story);
                return SaveStoryCore(vehicle.Stock, response.story, response.title);
            }

            catch (Exception ex) {
                return Json(new { success = false, message = "Error: " + ex.Message });
            }
        }

        [HttpPost]
        public JsonResult DeleteStory(string stock) {
            try {
                InventoryService.DeleteStory(stock);
                SyncCachedStoryForStock(stock, null, null);
                return Json(new { success = true, message = "Story deleted successfully." });
            }

            catch (Exception ex) {
                return Json(new { success = false, message = "Error: " + ex.Message });
            }
        }

        [HttpGet]
        public ActionResult GetDataOneDetails(string stock) {
            var normalizedStock = (stock ?? string.Empty).Trim();
            var vehicle = FindCachedVehicleForStock(normalizedStock);

            if (vehicle == null) {
                RefreshModel(includeHiddenInventory: true);
                vehicle = FindCachedVehicleForStock(normalizedStock);
            }

            if (vehicle == null)
                return HttpNotFound();

            Model.CurrentVehicle.VehicleDetails = vehicle;
            Model.CurrentVehicle.VehicleDataOneDetails = GetDecodedData(stock);

            return PartialView("_DetailsDataOne", Model);
        }

        [HttpPost]
        public JsonResult SaveStory(SaveStoryRequest request) {
            var stock = request?.Stock;
            var story = request?.Story;
            var title = request?.Title;
            return SaveStoryCore(stock, story, title);
        }

        private JsonResult SaveStoryCore(string stock, string story, string title) {
            try {
                var normalizedStock = (stock ?? string.Empty).Trim().ToUpperInvariant();
                if (string.IsNullOrWhiteSpace(normalizedStock)) {
                    return Json(new { success = false, message = "Stock is required." });
                }

                var sanitizedTitle = string.IsNullOrWhiteSpace(title) ? string.Empty : HttpUtility.HtmlEncode(title.Trim());
                var sanitizedStory = SecuritySanitizer.SanitizeRichHtml(story);

                InventoryService.SaveStory(normalizedStock, sanitizedStory, sanitizedTitle);
                SyncCachedStoryForStock(normalizedStock, sanitizedTitle, sanitizedStory);
                return Json(new { success = true, message = "Story saved successfully.", Title = sanitizedTitle, Story = sanitizedStory });
            }

            catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine("ERROR: " + ex.Message);
                return Json(new { success = false, message = "Error: " + ex.Message });
            }
        }

        [HttpPost]
        public async Task<ActionResult> ReStoryAll() {
            try {
                Model.Inventory.Vehicles = Model.Inventory.All;

                foreach (var vehicle in Model.Inventory.Vehicles) {
                    if (vehicle.Story == null || (vehicle.Story != null && vehicle.Story.HtmlContent.ToUpper().Contains("ERROR"))) {
                        var story = await GetChatGptResponse(GetPrompt(vehicle));
                        var response = SplitResponse(story);
                        var sanitizedTitle = string.IsNullOrWhiteSpace(response.title) ? string.Empty : HttpUtility.HtmlEncode(response.title.Trim());
                        var sanitizedStory = SecuritySanitizer.SanitizeRichHtml(response.story);

                        InventoryService.SaveStory(vehicle.Stock, sanitizedStory, sanitizedTitle);
                        SyncCachedStoryForStock(vehicle.Stock, sanitizedTitle, sanitizedStory);

                        // Add a delay to avoid hitting RPM limits
                        await Task.Delay(1100); // ~55 requests/min
                    }
                }

                return Json(new { success = true, message = "Story created successfully." });
            }
            catch (Exception ex) {
                return Json(new { success = false, message = "Error: " + ex.Message });
            }
        }

        [HttpPost]
        public ActionResult DecodeAll() {
            try {
                Model.Inventory.Vehicles = Model.Inventory.All;

                foreach (var vehicle in Model.Inventory.Vehicles) {
                    if (vehicle.DataOne == null) {
                        var details = VinDecoderService.DecodeVin(vehicle.VIN, dataOneApiKey, dataOneSecretApiKey);
                        InventoryService.SaveDataOneDetails(vehicle.Stock, details);
                    }
                }

                return Json(new { success = true, message = "DataOne decoded successfully." });
            }
            catch (Exception ex) {
                return Json(new { success = false, message = "Error: " + ex.Message });
            }
        }

        [HttpPost]
        public ActionResult DecodeDataOne(string vin) {
            try {
                string stock = Model.Inventory.All.Where(m => m.VIN == vin).FirstOrDefault()?.Stock;
                var details = VinDecoderService.DecodeVin(vin, dataOneApiKey, dataOneSecretApiKey);
                InventoryService.SaveDataOneDetails(stock, details);


                return Json(new { success = true, message = "DataOne decoded  successfully." });
            }
            catch (Exception ex) {
                return Json(new { success = false, message = "Error: " + ex.Message });
            }
        }

        [HttpGet]
        public ActionResult DecodeDataOneByAnyVin(string vin)
        {
            try
            {
                var details = VinDecoderService.DecodeVin(vin, dataOneApiKey, dataOneSecretApiKey);
                var vehicle = Model.Inventory.All?.FirstOrDefault(m => m.VIN == vin);
                Model.CurrentVehicle.VehicleDetails = vehicle;
                Model.CurrentVehicle.VehicleDataOneDetails = Models.GTX.SetDecodedData(details);
                return PartialView("_DetailsDataOne", Model);
            }
            catch (Exception ex)
            {
                base.Log(ex);
                return new HttpStatusCodeResult(500, "Unable to decode VIN.");
            }
        }

        [HttpGet]
        public JsonResult DecodeInventoryVehicleByVin(string vin) {
            try {
                var normalizedVin = NormalizeInventoryText(vin).ToUpperInvariant();
                var validationMessage = GetVinValidationMessage(normalizedVin);
                if (!string.IsNullOrWhiteSpace(validationMessage)) {
                    Response.StatusCode = 400;
                    return Json(new { success = false, message = validationMessage }, JsonRequestBehavior.AllowGet);
                }

                var details = VinDecoderService.DecodeVin(normalizedVin, dataOneApiKey, dataOneSecretApiKey);
                var decoded = Models.GTX.SetDecodedData(details);
                var vehicle = CreateInventoryVehicleFromDecodedData(normalizedVin, decoded);

                if (vehicle == null) {
                    Response.StatusCode = 404;
                    return Json(new { success = false, message = "No inventory details were decoded for this VIN." }, JsonRequestBehavior.AllowGet);
                }

                return Json(new {
                    success = true,
                    vehicle = ToInventoryEditorDecodeResponse(vehicle)
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex) {
                Response.StatusCode = 500;
                Log(ex);
                return Json(new { success = false, message = "VIN decode failed: " + ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        [HttpPost]
        public JsonResult DeleteDataOne(string stock) {
            try {
                var normalizedStock = (stock ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(normalizedStock)) {
                    return Json(new { success = false, message = "Stock is required." });
                }

                VinDecoderService.DeleteDataOneDetails(normalizedStock);
                SyncCachedDataOneForStock(normalizedStock, null);

                return Json(new {
                    success = true,
                    message = "DataOne details were deleted successfully.",
                    stock = normalizedStock,
                    hasDataOne = false
                });
            }

            catch (Exception ex) {
                return Json(new { success = false, message = "Error: " + ex.Message });
            }
        }

        [HttpGet]
        public JsonResult GetUpdatedItems() {
            RefreshModel(includeHiddenInventory: true);

            return new JsonResult
            {
                Data = Model.Inventory.Vehicles,
                JsonRequestBehavior = JsonRequestBehavior.AllowGet,
                MaxJsonLength = int.MaxValue   // or a big number you’re comfortable with
            };
        }

        [HttpPost]
        public JsonResult SaveInventoryVehicle(InventoryVehicleSaveRequest request) {
            try {
                var validationErrors = ValidateInventoryVehicleSaveRequest(request);
                if (validationErrors.Any()) {
                    Response.StatusCode = 400;
                    return Json(new { success = false, message = string.Join(" ", validationErrors) });
                }

                var vehicle = ToInventoryVehicle(request);
                var result = InventoryService.SaveInventoryVehicle(Models.GTX.ToDTO(vehicle), request?.OriginalStock);
                vehicle.TransmissionWord = Models.GTX.WordIt(vehicle.Transmission);
                var savedVehicle = DecideImages(new[] { vehicle }).FirstOrDefault();

                InvalidateInventoryCaches();

                return new JsonResult {
                    Data = new {
                        success = true,
                        message = GetInventorySaveMessage(result.Status),
                        status = (int)result.Status,
                        statusText = GetInventorySaveStatusText(result.Status),
                        vehicle = savedVehicle
                    },
                    MaxJsonLength = int.MaxValue
                };
            }
            catch (Exception ex) {
                Response.StatusCode = 500;
                Log(ex);
                return Json(new { success = false, message = "Inventory save failed: " + ex.Message });
            }
        }

        private static void InvalidateInventoryCaches() {
            AppCache.Remove(Constants.INVENTORY_CACHE);
            AppCache.Remove(Constants.CATEGORIES_CACHE);
            AppCache.Remove(Constants.FILTERS_CACHE);
        }

        private static List<string> ValidateInventoryVehicleSaveRequest(InventoryVehicleSaveRequest request) {
            var errors = new List<string>();
            if (request == null) {
                errors.Add("Inventory details are required.");
                return errors;
            }

            if (string.IsNullOrWhiteSpace(request.Stock)) {
                errors.Add("Stock is required.");
            }

            foreach (var limit in InventoryStringLimits) {
                var value = GetInventoryRequestValue(request, limit.Key);
                if (!string.IsNullOrEmpty(value) && value.Trim().Length > limit.Value) {
                    errors.Add($"{SplitInventoryFieldName(limit.Key)} must be {limit.Value} characters or fewer.");
                }
            }

            foreach (var field in InventoryIntegerFields) {
                var value = GetInventoryRequestValue(request, field);
                if (!IsValidInventoryInt(value)) {
                    errors.Add($"{SplitInventoryFieldName(field)} must be a whole number inside the SQL int range.");
                }
            }

            return errors;
        }

        private static Models.GTX ToInventoryVehicle(InventoryVehicleSaveRequest request) {
            return new Models.GTX {
                Stock = NormalizeInventoryText(request.Stock).ToUpperInvariant(),
                Year = ParseInventoryInt(request.Year),
                Make = NormalizeInventoryText(request.Make),
                Model = NormalizeInventoryText(request.Model),
                VIN = NormalizeInventoryText(request.VIN).ToUpperInvariant(),
                Mileage = ParseInventoryInt(request.Mileage),
                Cylinders = ParseInventoryInt(request.Cylinders),
                Weight = ParseInventoryInt(request.Weight),
                Color = NormalizeInventoryText(request.Color),
                Color2 = NormalizeInventoryText(request.Color2),
                Features = NormalizeInventoryText(request.Features),
                RetailPrice = ParseInventoryInt(request.RetailPrice),
                InternetPrice = ParseInventoryInt(request.InternetPrice),
                DriveTrain = NormalizeInventoryText(request.DriveTrain),
                LocationCode = NormalizeInventoryText(request.LocationCode).ToUpperInvariant(),
                Body = NormalizeInventoryText(request.Body),
                Engine = NormalizeInventoryText(request.Engine),
                Transmission = NormalizeInventoryText(request.Transmission).ToUpperInvariant(),
                PurchaseDate = NormalizeInventoryText(request.PurchaseDate),
                ArrivalDate = NormalizeInventoryText(request.ArrivalDate),
                FuelType = NormalizeInventoryText(request.FuelType),
                TransmissionSpeed = ParseInventoryInt(request.TransmissionSpeed),
                VehicleType = NormalizeInventoryText(request.VehicleType),
                VehicleStyle = NormalizeInventoryText(request.VehicleStyle),
                SetToUpload = NormalizeInventoryUploadFlag(request.SetToUpload)
            };
        }

        private static string GetInventoryRequestValue(InventoryVehicleSaveRequest request, string field) {
            var property = typeof(InventoryVehicleSaveRequest).GetProperty(field);
            return property == null ? string.Empty : property.GetValue(request, null) as string;
        }

        private static bool IsValidInventoryInt(string value) {
            var normalized = NormalizeInventoryNumberText(value);
            if (string.IsNullOrWhiteSpace(normalized)) {
                return true;
            }

            int parsed;
            return int.TryParse(normalized, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed);
        }

        private static int ParseInventoryInt(string value) {
            var normalized = NormalizeInventoryNumberText(value);
            if (string.IsNullOrWhiteSpace(normalized)) {
                return 0;
            }

            int parsed;
            return int.TryParse(normalized, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed) ? parsed : 0;
        }

        private static string NormalizeInventoryNumberText(string value) {
            return (value ?? string.Empty).Trim().Replace(",", string.Empty);
        }

        private static string NormalizeInventoryText(string value) {
            return (value ?? string.Empty).Trim();
        }

        private static string NormalizeInventoryUploadFlag(string value) {
            var normalized = NormalizeInventoryText(value).ToUpperInvariant();
            return string.IsNullOrWhiteSpace(normalized) ? "Y" : normalized;
        }

        private static string SplitInventoryFieldName(string field) {
            return Regex.Replace(field ?? string.Empty, "([a-z])([A-Z])", "$1 $2");
        }

        private static string GetInventorySaveMessage(InventoryUploadStatus status) {
            switch (status) {
                case InventoryUploadStatus.Insert:
                    return "Inventory item added.";
                case InventoryUploadStatus.Update:
                    return "Inventory item updated.";
                default:
                    return "No inventory changes detected.";
            }
        }

        private static string GetInventorySaveStatusText(InventoryUploadStatus status) {
            switch (status) {
                case InventoryUploadStatus.Insert:
                    return "Added";
                case InventoryUploadStatus.Update:
                    return "Updated";
                default:
                    return "Skipped";
            }
        }

        private static string GetVinValidationMessage(string vin) {
            if (string.IsNullOrWhiteSpace(vin)) {
                return "VIN is required.";
            }

            if (vin.Length != 17) {
                return "VIN must be exactly 17 characters.";
            }

            if (Regex.IsMatch(vin, "[IOQ]", RegexOptions.IgnoreCase)) {
                return "VIN cannot contain I, O, or Q.";
            }

            return string.Empty;
        }

        private static object ToInventoryEditorDecodeResponse(Models.GTX vehicle) {
            return new {
                VIN = vehicle.VIN,
                Year = FormatInventoryInt(vehicle.Year),
                Make = vehicle.Make,
                Model = vehicle.Model,
                RetailPrice = FormatInventoryInt(vehicle.RetailPrice),
                DriveTrain = vehicle.DriveTrain,
                Body = vehicle.Body,
                Engine = vehicle.Engine,
                Transmission = vehicle.Transmission,
                FuelType = vehicle.FuelType,
                TransmissionSpeed = FormatInventoryInt(vehicle.TransmissionSpeed),
                VehicleType = vehicle.VehicleType,
                VehicleStyle = vehicle.VehicleStyle,
                Cylinders = FormatInventoryInt(vehicle.Cylinders)
            };
        }

        private static string FormatInventoryInt(int value) {
            return value == 0 ? string.Empty : value.ToString(CultureInfo.InvariantCulture);
        }

        private static Models.GTX CreateInventoryVehicleFromDecodedData(string vin, DecodedData decoded) {
            var style = GetFirstDecodedStyle(decoded);
            if (style == null) {
                return null;
            }

            var basic = style.BasicData;
            var engine = style.Engines?.Items?.FirstOrDefault();
            var transmission = style.Transmissions?.Items?.FirstOrDefault();
            var epaFuel = style.EpaFuelEfficiency?.Records?.FirstOrDefault()?.FuelType;

            return new Models.GTX {
                VIN = LimitInventoryText("VIN", vin),
                Year = ParseDecodedInventoryInt(basic?.Year),
                Make = LimitInventoryText("Make", basic?.Make),
                Model = LimitInventoryText("Model", basic?.Model),
                RetailPrice = ParseDecodedInventoryInt(style.Pricing?.Msrp),
                DriveTrain = LimitInventoryText("DriveTrain", basic?.DriveType),
                Body = LimitInventoryText("Body", FirstInventoryText(basic?.BodyType, basic?.OemBodyStyle, basic?.BodySubtype)),
                Engine = LimitInventoryText("Engine", BuildDecodedEngineText(engine)),
                Transmission = LimitInventoryText("Transmission", MapDecodedTransmissionCode(transmission)),
                FuelType = LimitInventoryText("FuelType", FirstInventoryText(engine?.FuelType, epaFuel)),
                TransmissionSpeed = ParseDecodedInventoryInt(transmission?.Gears),
                VehicleType = LimitInventoryText("VehicleType", basic?.VehicleType),
                VehicleStyle = LimitInventoryText("VehicleStyle", NormalizeDecodedStyleText(FirstInventoryText(basic?.Trim, style.Name, basic?.OemBodyStyle))),
                Cylinders = ParseDecodedInventoryInt(engine?.IceCylinders)
            };
        }

        private static GTX.Models.Style GetFirstDecodedStyle(DecodedData decoded) {
            return decoded?.QueryResponses?.Items?
                .SelectMany(response => response?.UsMarketData?.UsStyles?.Styles ?? Enumerable.Empty<GTX.Models.Style>())
                .FirstOrDefault(style => style?.BasicData != null);
        }

        private static string BuildDecodedEngineText(Engine engine) {
            if (engine == null) {
                return string.Empty;
            }

            var displacement = NormalizeInventoryText(engine.IceDisplacement);
            if (!string.IsNullOrWhiteSpace(displacement)) {
                return NormalizeDecodedEngineText(displacement);
            }

            return NormalizeDecodedEngineText(FirstInventoryText(engine.MarketingName, engine.Name));
        }

        private static string NormalizeDecodedEngineText(string value) {
            var text = NormalizeInventoryText(value).ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(text)) {
                return string.Empty;
            }

            var match = Regex.Match(text, @"\b\d+(?:\.\d+)?\s*L\b", RegexOptions.IgnoreCase);
            if (match.Success) {
                return Regex.Replace(match.Value.ToUpperInvariant(), @"\s+", string.Empty);
            }

            if (Regex.IsMatch(text, @"^\d+(?:\.\d+)?$")) {
                return text + "L";
            }

            return text;
        }

        private static string NormalizeDecodedStyleText(string value) {
            var text = Regex.Replace(NormalizeInventoryText(value), @"\s+", " ").Trim();
            return text.ToUpperInvariant();
        }

        private static string MapDecodedTransmissionCode(Transmission transmission) {
            var text = FirstInventoryText(transmission?.Type, transmission?.DetailType, transmission?.Name).ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(text)) {
                return string.Empty;
            }

            if (text.Length == 1 && "AMCT".Contains(text)) {
                return text;
            }

            if (text.Contains("CONTINU") || text.Contains("CVT")) {
                return "C";
            }

            if (text.Contains("MANUAL")) {
                return "M";
            }

            if (text.Contains("TRANSVERSE")) {
                return "T";
            }

            if (text.Contains("AUTO")) {
                return "A";
            }

            return string.Empty;
        }

        private static string FirstInventoryText(params string[] values) {
            return (values ?? Array.Empty<string>())
                .Select(NormalizeInventoryText)
                .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
        }

        private static string LimitInventoryText(string field, string value) {
            var normalized = NormalizeInventoryText(value);
            int maxLength;
            if (!string.IsNullOrEmpty(normalized) &&
                InventoryStringLimits.TryGetValue(field, out maxLength) &&
                normalized.Length > maxLength) {
                return normalized.Substring(0, maxLength);
            }

            return normalized;
        }

        private static int ParseDecodedInventoryInt(string value) {
            var normalized = NormalizeInventoryNumberText(value).Replace("$", string.Empty);
            if (string.IsNullOrWhiteSpace(normalized)) {
                return 0;
            }

            decimal parsed;
            return decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out parsed)
                ? Convert.ToInt32(Math.Round(parsed, 0, MidpointRounding.AwayFromZero))
                : 0;
        }

        [HttpPost]
        public async Task<ActionResult> UploadInventoryFiles(IEnumerable<HttpPostedFileBase> files, string stock) {
            try {
                var normalizedStock = (stock ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(normalizedStock)) {
                    Response.StatusCode = 400;
                    return Json(new { success = false, message = "Stock is required." });
                }

                var uploadPath = CombineUnderInventoryImagesRoot(normalizedStock);
                Directory.CreateDirectory(uploadPath);

                var fileList = (files ?? Enumerable.Empty<HttpPostedFileBase>())
                    .Where(f => f != null && f.ContentLength > 0)
                    .ToList();

                var uploadedCount = 0;
                var skippedCount = 0;
                var failedFiles = new List<string>();

                foreach (var file in fileList) {
                    try {
                        var originalExtension = Path.GetExtension(file.FileName);
                        if (string.IsNullOrWhiteSpace(originalExtension) || !UploadImageExtensions.Contains(originalExtension)) {
                            skippedCount++;
                            continue;
                        }

                        var inputStream = file.InputStream;
                        if (inputStream.CanSeek) {
                            inputStream.Position = 0;
                        }

                        using (var image = new MagickImage(inputStream)) {
                            image.AutoOrient();
                            image.Orientation = OrientationType.TopLeft;
                            image.ColorSpace = ColorSpace.sRGB;

                            ResizeInventoryUploadImage(image);

                            var preservesTransparency = image.HasAlpha && !image.IsOpaque;
                            var targetExtension = preservesTransparency ? UploadPngExtension : UploadJpegExtension;
                            var fileName = BuildInventoryUploadFileName(file.FileName, targetExtension);
                            var fullPath = Path.Combine(uploadPath, fileName);

                            // Do not override existing files.
                            if (System.IO.File.Exists(fullPath)) {
                                skippedCount++;
                                continue;
                            }

                            ConfigureInventoryUploadOutput(image, preservesTransparency);

                            await image.WriteAsync(fullPath);
                            InventoryService.SaveImage(normalizedStock, fileName);
                        }

                        uploadedCount++;
                    }
                    catch (Exception fileEx) {
                        failedFiles.Add($"{file?.FileName}: {fileEx.Message}");
                    }
                }

                var images = InventoryService.GetImages(normalizedStock) ?? Array.Empty<Services.Image>();
                SyncCachedImagesForStock(normalizedStock, images);

                return Json(new {
                    success = failedFiles.Count == 0,
                    uploaded = uploadedCount,
                    skipped = skippedCount,
                    failed = failedFiles.Count,
                    errors = failedFiles,
                    images = ToUploadImageResponseDtos(images),
                    image = GetCachedLeadImageForStock(normalizedStock),
                    message = failedFiles.Count == 0 ? "Upload completed." : "Upload completed with some errors.",
                    Message = failedFiles.Count == 0 ? "Upload completed." : "Upload completed with some errors."
                });
            }
            catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine("Upload failed completely: " + ex.Message);
                Response.StatusCode = 500;
                return Json(new { success = false, message = "Upload failed: " + ex.Message });
            }
        }

        private static void ResizeInventoryUploadImage(MagickImage image) {
            image.Resize(new MagickGeometry(UploadImageMaxWidth, UploadImageMaxHeight) {
                Greater = true,
                IgnoreAspectRatio = false
            });
        }

        private static void ConfigureInventoryUploadOutput(MagickImage image, bool preservesTransparency) {
            image.Strip();
            image.Depth = 8;

            if (preservesTransparency) {
                image.ColorType = ColorType.TrueColorAlpha;
                image.Format = MagickFormat.Png32;
                image.Settings.SetDefine(MagickFormat.Png, "png:compression-level", UploadPngCompressionLevel.ToString());
                return;
            }

            image.BackgroundColor = MagickColors.White;
            image.Alpha(AlphaOption.Remove);
            image.ColorType = ColorType.TrueColor;
            image.Format = MagickFormat.Jpeg;
            image.Settings.Interlace = Interlace.Jpeg;
            image.Quality = UploadJpegQuality;
            image.Settings.SetDefine(MagickFormat.Jpeg, "sampling-factor", "4:2:0");
        }

        private static string BuildInventoryUploadFileName(string originalFileName, string extension) {
            var baseName = Path.GetFileNameWithoutExtension(originalFileName);
            baseName = Regex.Replace(baseName ?? string.Empty, @"[^\w\-. ]+", "-").Trim();
            baseName = Regex.Replace(baseName, @"\s+", "-");
            baseName = Regex.Replace(baseName, @"-+", "-").Trim('-', '.');

            if (string.IsNullOrWhiteSpace(baseName)) {
                baseName = "inventory-image";
            }

            if (baseName.Length > 80) {
                baseName = baseName.Substring(0, 80).Trim('-', '.');
            }

            if (string.IsNullOrWhiteSpace(baseName)) {
                baseName = "inventory-image";
            }

            return baseName + extension;
        }

        [HttpPost]
        public JsonResult SetDetails(string stock) {
            try {
                stock = (stock ?? string.Empty).Trim().ToUpperInvariant();
                Model.CurrentVehicle.VehicleDetails = Model.Inventory.All.FirstOrDefault(m => m.Stock == stock);
                return Json(new { success = true });
            }
            catch (Exception ex) {
                return Json(new { success = false, message = $"Error setting details: {ex.Message}" });
            }
        }

        public ActionResult OverlayModal(Guid id) {
            Services.Image image = InventoryService.GetImage(id);
            if (image == null) {
                return HttpNotFound($"Image {id} was not found.");
            }

            return PartialView("_OverlayModal", image);
        }

        [HttpPost]
        public JsonResult SaveOrder(Guid[] sorted) {
            try {
                if (sorted == null || sorted.Length == 0) {
                    return Json(new { success = false, message = "No images were provided to reorder." });
                }

                InventoryService.UpdateOrder(sorted);
                var firstImageId = sorted.FirstOrDefault(id => id != Guid.Empty);
                if (firstImageId != Guid.Empty) {
                    var firstImage = InventoryService.GetImage(firstImageId);
                    var stock = (firstImage?.Stock ?? string.Empty).Trim();

                    if (!string.IsNullOrWhiteSpace(stock)) {
                        var images = InventoryService.GetImages(stock) ?? Array.Empty<Services.Image>();
                        SyncCachedImagesForStock(stock, images);
                    }
                }

                return Json(new { success = true });
            }
            catch (Exception ex) {
                return Json(new { success = false, message = $"Error saving image order: {ex.Message}" });
            }
        }

        private void SyncCachedStoryForStock(string stock, string title, string storyHtml) {
            var stockKey = (stock ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(stockKey) || Model?.Inventory == null) {
                return;
            }

            var hasStory = !string.IsNullOrWhiteSpace(title) || !string.IsNullOrWhiteSpace(storyHtml);
            var normalizedTitle = title ?? string.Empty;
            var normalizedStory = storyHtml ?? string.Empty;

            lock (_storyCacheLock) {
                ApplyStoryToVehicles(Model.Inventory.All, stockKey, hasStory, normalizedTitle, normalizedStory);
                ApplyStoryToVehicles(Model.Inventory.Vehicles, stockKey, hasStory, normalizedTitle, normalizedStory);
            }
        }

        private static void ApplyStoryToVehicles(Models.GTX[] vehicles, string stock, bool hasStory, string title, string storyHtml) {
            if (vehicles == null || vehicles.Length == 0) {
                return;
            }

            foreach (var vehicle in vehicles) {
                if (vehicle == null || !string.Equals(vehicle.Stock, stock, StringComparison.OrdinalIgnoreCase)) {
                    continue;
                }

                vehicle.Story = hasStory
                    ? new Story {
                        Stock = stock,
                        Title = title,
                        HtmlContent = storyHtml,
                        DateCreated = DateTime.Now
                    }
                    : null;
            }
        }

        private void SyncCachedImagesForStock(string stock, Services.Image[] images) {
            var stockKey = (stock ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(stockKey) || Model?.Inventory == null) {
                return;
            }

            var normalizedImages = images ?? Array.Empty<Services.Image>();
            var defaultImage = $"{imageFolder}no-image-1.jpg";
            var leadImage = normalizedImages.Length > 0
                ? $"{imageFolder}{normalizedImages[0].Source}"
                : defaultImage;

            lock (_imageCacheLock) {
                ApplyImagesToVehicles(Model.Inventory.All, stockKey, normalizedImages, leadImage);
                ApplyImagesToVehicles(Model.Inventory.Vehicles, stockKey, normalizedImages, leadImage);
            }
        }

        private void SyncCachedDataOneForStock(string stock, DecodedData dataOne) {
            var stockKey = (stock ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(stockKey) || Model?.Inventory == null) {
                return;
            }

            lock (_dataOneCacheLock) {
                ApplyDataOneToVehicles(Model.Inventory.All, stockKey, dataOne);
                ApplyDataOneToVehicles(Model.Inventory.Vehicles, stockKey, dataOne);
            }
        }

        private static void ApplyDataOneToVehicles(Models.GTX[] vehicles, string stock, DecodedData dataOne) {
            if (vehicles == null || vehicles.Length == 0) {
                return;
            }

            foreach (var vehicle in vehicles) {
                if (vehicle == null || !string.Equals(vehicle.Stock, stock, StringComparison.OrdinalIgnoreCase)) {
                    continue;
                }

                vehicle.DataOne = dataOne;
            }
        }

        private Models.GTX FindCachedVehicleForStock(string stock) {
            var stockKey = (stock ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(stockKey) || Model?.Inventory == null) {
                return null;
            }

            return (Model.Inventory.Vehicles ?? Array.Empty<Models.GTX>())
                .Concat(Model.Inventory.All ?? Array.Empty<Models.GTX>())
                .FirstOrDefault(vehicle => vehicle != null && string.Equals(vehicle.Stock, stockKey, StringComparison.OrdinalIgnoreCase));
        }

        private string GetCachedLeadImageForStock(string stock) {
            return FindCachedVehicleForStock(stock)?.Image ?? $"{imageFolder}no-image-1.jpg";
        }

        private static UploadImageResponseDto[] ToUploadImageResponseDtos(IEnumerable<Services.Image> images) {
            return (images ?? Array.Empty<Services.Image>())
                .Select(m => new UploadImageResponseDto {
                    Id = m.Id,
                    Stock = m.Stock,
                    Source = m.Source,
                    Overlay = m.Overlay,
                    Order = m.Order
                })
                .ToArray();
        }

        private static void ApplyImagesToVehicles(Models.GTX[] vehicles, string stock, Services.Image[] images, string leadImage) {
            if (vehicles == null || vehicles.Length == 0) {
                return;
            }

            foreach (var vehicle in vehicles) {
                if (vehicle == null || !string.Equals(vehicle.Stock, stock, StringComparison.OrdinalIgnoreCase)) {
                    continue;
                }

                vehicle.Images = images;
                vehicle.Image = leadImage;
            }
        }

        [HttpPost]
        public JsonResult SaveOverlay(Guid id, string stock, string overlay, string imagePath) {
            try {
                if (id == Guid.Empty) {
                    return Json(new { success = false, message = "Invalid image id." });
                }

                var normalizedStock = (stock ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(normalizedStock) || string.IsNullOrWhiteSpace(overlay) || string.IsNullOrWhiteSpace(imagePath)) {
                    return Json(new { success = false, message = "Missing required overlay parameters." });
                }

                InventoryService.SaveOverlay(id, overlay);
                CreateImageWithOverlay(normalizedStock, imagePath, overlay);

                var images = InventoryService.GetImages(normalizedStock) ?? Array.Empty<Services.Image>();
                SyncCachedImagesForStock(normalizedStock, images);

                return Json(new {
                    success = true,
                    stock = normalizedStock,
                    images = ToUploadImageResponseDtos(images),
                    image = GetCachedLeadImageForStock(normalizedStock)
                });
            }
            catch (Exception ex) {
                return Json(new { success = false, message = $"Error saving overlay: {ex.Message}" });
            }
        }

        [HttpPost]
        public JsonResult DeleteOverlay(Guid id, string stock) {
            try {
                if (id == Guid.Empty) {
                    return Json(new { success = false, message = "Invalid image id." });
                }

                var image = InventoryService.GetImage(id);
                var normalizedStock = (stock ?? image?.Stock ?? string.Empty).Trim();
                InventoryService.DeleteOverlay(id);
                DeleteOverlayRenderedImageFile(image, normalizedStock);

                var images = string.IsNullOrWhiteSpace(normalizedStock)
                    ? Array.Empty<Services.Image>()
                    : InventoryService.GetImages(normalizedStock) ?? Array.Empty<Services.Image>();

                if (!string.IsNullOrWhiteSpace(normalizedStock)) {
                    SyncCachedImagesForStock(normalizedStock, images);
                }

                return Json(new {
                    success = true,
                    stock = normalizedStock,
                    images = ToUploadImageResponseDtos(images),
                    image = GetCachedLeadImageForStock(normalizedStock)
                });
            }
            catch (Exception ex) {
                return Json(new { success = false, message = $"Error deleting overlay: {ex.Message}" });
            }
        }

        private void DeleteOverlayRenderedImageFile(Services.Image image, string stock) {
            if (image == null) {
                return;
            }

            var source = image.Source ?? string.Empty;
            var resolvedStock = !string.IsNullOrWhiteSpace(stock) ? stock : image.Stock;
            var basePath = ResolveInventoryImagePhysicalPath(source);

            if ((string.IsNullOrWhiteSpace(basePath) || !System.IO.File.Exists(basePath)) &&
                !string.IsNullOrWhiteSpace(resolvedStock) &&
                !string.IsNullOrWhiteSpace(source)) {
                basePath = CombineUnderInventoryImagesRoot(resolvedStock, Path.GetFileName(source));
            }

            if (string.IsNullOrWhiteSpace(basePath)) {
                return;
            }

            var directory = Path.GetDirectoryName(basePath);
            if (string.IsNullOrWhiteSpace(directory)) {
                return;
            }

            var baseName = Path.GetFileNameWithoutExtension(basePath);
            if (baseName.EndsWith("-O", StringComparison.OrdinalIgnoreCase)) {
                baseName = baseName.Substring(0, baseName.Length - 2);
            }

            var overlayPath = Path.Combine(directory, baseName + "-O.png");
            if (System.IO.File.Exists(overlayPath)) {
                System.IO.File.Delete(overlayPath);
            }
        }

        [HttpPost]
        public JsonResult ApplyTerm(string term) {
            Model.CurrentFilter = null;
            Model.Inventory.Vehicles = ApplyTerms(term.Trim().ToUpper());
            Model.Inventory.Title = "Search";
            return Json(new { redirectUrl = Url.Action("Inventory") });
        }

        [HttpPost]
        public JsonResult DeleteImage(Guid id, string file, string stock) {
            var image = id != Guid.Empty ? InventoryService.GetImage(id) : null;
            var normalizedStock = (stock ?? image?.Stock ?? string.Empty).Trim();
            var path = ResolveInventoryImagePhysicalPath(file);

            if ((string.IsNullOrWhiteSpace(path) || !System.IO.File.Exists(path)) &&
                !string.IsNullOrWhiteSpace(normalizedStock) &&
                !string.IsNullOrWhiteSpace(file)) {
                path = CombineUnderInventoryImagesRoot(normalizedStock, Path.GetFileName(file));
            }

            if (System.IO.File.Exists(path)) {
                System.IO.File.Delete(path);
            }

            InventoryService.DeleteImage(id);

            var images = string.IsNullOrWhiteSpace(normalizedStock)
                ? Array.Empty<Services.Image>()
                : InventoryService.GetImages(normalizedStock) ?? Array.Empty<Services.Image>();

            if (!string.IsNullOrWhiteSpace(normalizedStock)) {
                SyncCachedImagesForStock(normalizedStock, images);
            }

            return Json(new {
                success = true,
                stock = normalizedStock,
                images = ToUploadImageResponseDtos(images),
                image = GetCachedLeadImageForStock(normalizedStock)
            });
        }

        [HttpPost]
        public JsonResult RotateImage(string file, string stock, int? degrees) {
            try {
                var path = ResolveInventoryImagePhysicalPath(file);

                if ((string.IsNullOrWhiteSpace(path) || !System.IO.File.Exists(path)) &&
                    !string.IsNullOrWhiteSpace(stock) &&
                    !string.IsNullOrWhiteSpace(file)) {
                    path = CombineUnderInventoryImagesRoot(stock, Path.GetFileName(file));
                }

                if (string.IsNullOrWhiteSpace(path) || !System.IO.File.Exists(path)) {
                    return Json(new { success = false, message = "Image file not found." });
                }

                using (var image = new MagickImage(path)) {
                    var rotationDegrees = (degrees.HasValue && degrees.Value == -90) ? -90 : 90;

                    image.AutoOrient();
                    image.Orientation = OrientationType.TopLeft;
                    TrimTransparentBorder(image);

                    image.Rotate(rotationDegrees);
                    image.Orientation = OrientationType.TopLeft;
                    TrimTransparentBorder(image);
                    image.ResetPage();

                    image.Write(path);
                }

                return Json(new { success = true });
            }
            catch (Exception ex) {
                return Json(new { success = false, message = $"Error rotating image: {ex.Message}" });
            }
        }

        private static void TrimTransparentBorder(MagickImage image)
        {
            if (image == null)
            {
                return;
            }

            image.Alpha(AlphaOption.Set);
            image.BackgroundColor = MagickColors.Transparent;
            image.ColorFuzz = new Percentage(0);
            image.Trim();
            image.ResetPage();
        }

        [HttpPost]
        public ActionResult RestoreBackUpInventory()
        {
            var result = Utility.XMLHelpers.XmlRepository.GetInventory();
            var vehicles = result.Vehicles.Where(m => m.SetToUpload == "Y").OrderBy(m => m.Make).ThenBy(m => m.Model).ToArray();

            InventoryService.AddInventory(Models.GTX.ToDTOs(vehicles));

            TerminateSession();
            return RedirectToAction("Index", "Home");
        }

        [HttpPost]
        public ActionResult DeleteImages(string stock) {
            var normalizedStock = (stock ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(normalizedStock)) {
                return Json(new { success = false, message = "Stock is required." });
            }

            try {
                var path = CombineUnderInventoryImagesRoot(normalizedStock);
                var imageFiles = Directory.Exists(path)
                    ? Directory.GetFiles(path).Where(file => UploadImageExtensions.Contains(Path.GetExtension(file))).ToArray()
                    : Array.Empty<string>();

                foreach (string file in imageFiles) {
                    System.IO.File.Delete(file);
                }

                var images = Array.Empty<Services.Image>();
                InventoryService.DeleteImages(normalizedStock);
                SyncCachedImagesForStock(normalizedStock, images);

                return Json(new {
                    success = true,
                    message = "All files deleted successfully.",
                    stock = normalizedStock,
                    images = ToUploadImageResponseDtos(images),
                    image = GetCachedLeadImageForStock(normalizedStock)
                });
            }

            catch (Exception ex) {
                return Json(new { success = false, message = "Error: " + ex.Message });
            }
        }

        private static Bitmap ToNonIndexedBitmap(System.Drawing.Image src) {
            if ((src.PixelFormat & PixelFormat.Indexed) == 0)
                return new Bitmap(src);

            var bmp = new Bitmap(src.Width, src.Height, PixelFormat.Format32bppArgb);
            bmp.SetResolution(src.HorizontalResolution, src.VerticalResolution);
            using (var g = Graphics.FromImage(bmp))
                g.DrawImageUnscaled(src, 0, 0);
            return bmp;
        }

        private void CreateImageWithOverlay(string stock, string baseImagePath, string overlayJson) {
            string baseImage = ResolveInventoryImagePhysicalPath(baseImagePath);
            if (string.IsNullOrWhiteSpace(baseImage) || !System.IO.File.Exists(baseImage))
            {
                return;
            }

            string dir = Path.GetDirectoryName(baseImage);
            string filename = Path.Combine(dir, Path.GetFileNameWithoutExtension(baseImage) + "-O.png");

            using (var src = System.Drawing.Image.FromFile(baseImage))
            using (var image = ToNonIndexedBitmap(src)) // <-- guarantees 32bpp
            using (var graphics = Graphics.FromImage(image)) {
                graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                graphics.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
                graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

                // Parse JSON
                JObject overlayObj = JObject.Parse(overlayJson);
                var overlay = overlayObj["overlay"];
                var bgColor = GetOverlayBackgroundColor(overlay);
                var overlayOpacity = GetOverlayBackgroundOpacity(overlay);

                int overlayHeight = image.Height / 8;
                var overlayRect = new Rectangle(0, image.Height - overlayHeight, image.Width, overlayHeight);
                int alpha = (int)Math.Round(overlayOpacity * 255);
                var bgColorWithAlpha = Color.FromArgb(alpha, bgColor.R, bgColor.G, bgColor.B);

                using (var rectBrush = new SolidBrush(bgColorWithAlpha))
                    graphics.FillRectangle(rectBrush, overlayRect);

                // Draw text
                foreach (var child in overlay["children"]) {
                    string text = (string)child["text"];
                    string style = (string)child["style"];

                    var (font, color) = GetFontAndColorFromStyle(image.Width, overlayHeight, style);

                    using (font)
                    using (var textBrush = new SolidBrush(color))
                    using (var format = new StringFormat {
                        Alignment = StringAlignment.Center,
                        LineAlignment = StringAlignment.Center,
                        Trimming = StringTrimming.EllipsisWord,
                        FormatFlags = StringFormatFlags.LineLimit
                    }) {
                        var textRect = new RectangleF(
                            20,                                  // left padding
                            image.Height - overlayHeight,        // y
                            image.Width - 40,                    // width (left+right padding)
                            overlayHeight                        // height
                        );

                        graphics.DrawString(text, font, textBrush, textRect, format);
                    }
                }

                image.Save(filename, ImageFormat.Png);
            }

            InventoryService.SaveImage(stock, Path.GetFileName(filename));
        }

        private async Task<string> GetChatGptResponse(string prompt) {
            var apiUrl = "https://api.openai.com/v1/chat/completions";

            using (var httpClient = new HttpClient()) {
                httpClient.DefaultRequestHeaders.Clear();
                httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {openAiApiKey}");

                var requestBody = new {
                    model = "gpt-4o",
                    messages = new[]
                    {
                        new { role = "user", content = prompt }
                    },
                    max_tokens = 700,
                    temperature = 0.9
                };

                var content = new StringContent(JsonConvert.SerializeObject(requestBody), Encoding.UTF8, "application/json");

                var response = await httpClient.PostAsync(apiUrl, content);

                if (response.IsSuccessStatusCode) {
                    var jsonResponse = await response.Content.ReadAsStringAsync();
                    dynamic result = JsonConvert.DeserializeObject(jsonResponse);

                    return result.choices[0].message.content.ToString();
                }
                else {
                    return $"Error: {response.StatusCode}";
                }
            }
        }

        private string GetPrompt(Models.GTX vehicle) {
            var reps = string.Empty;
            var representatives = Model.Employers.Where(m => m.Position.Contains("Sales")).Select(m => $"{m.FirstName} { m.LastName}").ToArray();
            if (representatives != null) {
                reps = string.Join(", ", representatives);
            }
            string car = $"{vehicle.Year} {vehicle.Make} {vehicle.Model} {vehicle.VehicleStyle}";
            var features = $"{vehicle.Features}";
            string prompt = $@"
    You are an expert used cars automotive sales person with a high level of technical skill. Write a short captivating, imaginative, vivid, and engaging story in HTML format for the following car:
    Car: {car}  
    Features: {features}
    General: {car} is being sold by the GTX Autogroup here in Cincinnati Ohio area.
    Our sales crew: {reps} will help you will help you buy a perfect car you need.

    Your response must:
    1. Start with a catchy **title inside <title> tags** (for example: <title>The Electric Dream</title>).
    2. Write a minimum of **5 sentences**, each inside a separate <p class='p-story'> tag.
    3. Write in very technical tone with a touch sales person can be to make story vivid, rich, and atmospheric.
    4. Mention at least **5 car features** from the provided list and wrap each feature in <strong class='strong-story'> tags as well as the car.
    5. Do **not use double quotes** anywhere in the story.
    6. End the story with a sense of joy, adventure, opportunity.

    The output should be **only the HTML story** without any extra text before or after.
    Please do not place any other characters like **``` and **```html text in front of the output.
    Do not place any **<html>**, **<body>** and **<head>** tags
    ";
            return prompt;
        }

        private (string story, string title) SplitResponse(string response) {
            string story;
            string title = string.Empty;

            // Гамно remover
            story = response.Replace("```html", "").Replace("```", "").Replace("\"", "");
            story = story.Replace("<html>", "").Replace("</html>", "");
            story = story.Replace("<head>", "").Replace("</head>", "");
            story = story.Replace("<body>", "").Replace("</body>", "");

            string pattern = @"<title>\s*(.*?)\s*</title>";
            Match match = Regex.Match(story, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);

            if (match.Success) {
                title = (match.Groups[1].Value ?? string.Empty).Trim();  // Extracted inner text

                // Remove the entire <h5>...</h5> from the HTML
                story = Regex.Replace(story, pattern, string.Empty, RegexOptions.IgnoreCase | RegexOptions.Singleline);
            }

            return (story, title);
        }

        private Color GetColorFromStyle(string style) {
            var styles = ParseCssStyle(style);
            return styles.TryGetValue("background-color", out var color)
                ? ParseCssColor(color, Color.Transparent)
                : Color.Transparent;
        }

        private Color GetOverlayBackgroundColor(JToken overlay) {
            var color = (string)overlay?["backgroundColor"];
            if (!string.IsNullOrWhiteSpace(color)) {
                return ParseCssColor(color, Color.Transparent);
            }

            return GetColorFromStyle((string)overlay?["style"]);
        }

        private double GetOverlayBackgroundOpacity(JToken overlay) {
            var opacity = (string)overlay?["backgroundOpacity"];
            if (string.IsNullOrWhiteSpace(opacity)) {
                var styles = ParseCssStyle((string)overlay?["style"]);
                styles.TryGetValue("background-opacity", out opacity);
                if (string.IsNullOrWhiteSpace(opacity)) {
                    styles.TryGetValue("opacity", out opacity);
                }
            }

            if (string.IsNullOrWhiteSpace(opacity)) {
                return 1d;
            }

            opacity = opacity.Trim();
            if (opacity.EndsWith("%", StringComparison.Ordinal)) {
                var percentValue = opacity.Substring(0, opacity.Length - 1);
                if (double.TryParse(percentValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var percent)) {
                    return Math.Max(0d, Math.Min(1d, percent / 100d));
                }
            }

            return double.TryParse(opacity, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
                ? Math.Max(0d, Math.Min(1d, value))
                : 1d;
        }

        private (Font font, Color color) GetFontAndColorFromStyle(int width, int overlayHeight, string style) {
            var styles = ParseCssStyle(style);
            FontStyle fontStyle = FontStyle.Regular;

            if (styles.TryGetValue("font-style", out var cssFontStyle) &&
                cssFontStyle.Equals("italic", StringComparison.OrdinalIgnoreCase)) {
                fontStyle |= FontStyle.Italic;
            }

            if (styles.TryGetValue("font-weight", out var cssFontWeight) &&
                IsBoldFontWeight(cssFontWeight)) {
                fontStyle |= FontStyle.Bold;
            }

            var size = styles.TryGetValue("font-size", out var cssFontSize)
                ? ParseCssFontSize(cssFontSize, width, overlayHeight)
                : Math.Max(16f, overlayHeight * 0.45f);

            var color = styles.TryGetValue("color", out var cssColor)
                ? ParseCssColor(cssColor, Color.Black)
                : Color.Black;

            Font font = new Font("Arial", size, fontStyle, GraphicsUnit.Pixel);
            return (font, color);
        }

        private static Dictionary<string, string> ParseCssStyle(string style) {
            var styles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(style)) {
                return styles;
            }

            foreach (var rule in style.Split(';')) {
                var separator = rule.IndexOf(':');
                if (separator <= 0) {
                    continue;
                }

                var property = rule.Substring(0, separator).Trim();
                var value = rule.Substring(separator + 1).Trim();
                if (!string.IsNullOrWhiteSpace(property) && !string.IsNullOrWhiteSpace(value)) {
                    styles[property] = value;
                }
            }

            return styles;
        }

        private static bool IsBoldFontWeight(string value) {
            if (string.IsNullOrWhiteSpace(value)) {
                return false;
            }

            value = value.Trim();
            if (value.Equals("bold", StringComparison.OrdinalIgnoreCase) ||
                value.Equals("bolder", StringComparison.OrdinalIgnoreCase)) {
                return true;
            }

            return int.TryParse(value, out var weight) && weight >= 600;
        }

        private static float ParseCssFontSize(string value, int width, int overlayHeight) {
            var fallback = Math.Max(16f, overlayHeight * 0.45f);
            if (string.IsNullOrWhiteSpace(value)) {
                return fallback;
            }

            var match = Regex.Match(value.Trim(), @"^(\d+(?:\.\d+)?)(vw|px|pt|em|rem)?$", RegexOptions.IgnoreCase);
            if (!match.Success ||
                !float.TryParse(match.Groups[1].Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var number)) {
                return fallback;
            }

            var unit = match.Groups[2].Success ? match.Groups[2].Value.ToLowerInvariant() : "px";
            float pixels;

            switch (unit) {
                case "vw":
                    pixels = number * (width / 100f) * 2.3f;
                    break;
                case "pt":
                    pixels = number * 96f / 72f;
                    break;
                case "em":
                case "rem":
                    pixels = number * 16f;
                    break;
                default:
                    pixels = number;
                    break;
            }

            var max = Math.Max(18f, overlayHeight * 0.8f);
            return Math.Max(12f, Math.Min(pixels, max));
        }

        private static Color ParseCssColor(string value, Color fallback) {
            if (string.IsNullOrWhiteSpace(value)) {
                return fallback;
            }

            value = value.Trim();
            try {
                return ColorTranslator.FromHtml(value);
            }
            catch {
                var color = Color.FromName(value);
                return color.IsKnownColor || color.IsNamedColor || color.A > 0
                    ? color
                    : fallback;
            }

        }

        [AllowAnonymous]
        [HttpGet]
        [OutputCache(Duration = 86400, VaryByParam = "text;stock;vin")] // cache for speed
        public ActionResult Qr(string text, string stock, string vin)
        {
            text = (text ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(text))
            {
                stock = (stock ?? string.Empty).Trim();
                vin = (vin ?? string.Empty).Trim();

                if (string.IsNullOrWhiteSpace(stock))
                    return new HttpStatusCodeResult(400, "QR text or stock is required");

                var query = "stock=" + HttpUtility.UrlEncode(stock);
                if (!string.IsNullOrWhiteSpace(vin))
                {
                    query += "&QR=" + HttpUtility.UrlEncode(vin);
                }

                text = "https://usedcarscincinnati.com/Inventory/Details?" + query;
            }

            if (text.Length > QrTextMaxLength)
                return new HttpStatusCodeResult(400, "QR text is too long");

            using (var qrGenerator = new QRCodeGenerator())
            using (var qrData = qrGenerator.CreateQrCode(text, QRCodeGenerator.ECCLevel.Q))
            using (var qrCode = new PngByteQRCode(qrData))
            {
                var foreground = System.Drawing.ColorTranslator.FromHtml("#000000"); // dark
                var background = System.Drawing.ColorTranslator.FromHtml("#FFFFFF"); // light

                byte[] bytes = qrCode.GetGraphic(
                    pixelsPerModule: 10,
                    darkColor: foreground,
                    lightColor: background,
                    drawQuietZones: true
                );
                return File(bytes, "image/png");
            }
        }
    }
}


