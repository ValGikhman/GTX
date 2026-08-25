using GTX.Common;
using GTX.Models;
using Microsoft.VisualBasic.FileIO;
using Services;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Xml.Linq;
using System.Xml.Serialization;
using Utility.XMLHelpers;

namespace GTX.Controllers
{
    [RequireAdminRole(RequiredRole = CommonUnit.Roles.Owner)]
    public class InventoryManagementController : BaseController
    {
        private const string HeaderFileVirtualPath = "~/App_Data/Inventory/header.csv";
        private const string InventoryRollbackTokenSessionKey = "InventoryManagement.RollbackToken";

        private static byte[] _cachedHeaderBytes;
        private static readonly object _headerLock = new object();

        private static readonly IDictionary<string, int> InventoryCsvTextLimits =
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
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
                { "PurchaseDate", 50 },
                { "ArrivalDate", 50 },
                { "FuelType", 50 },
                { "VehicleType", 50 },
                { "VehicleStyle", 50 },
                { "SetToUpload", 1 }
            };

        private static readonly string[] InventoryCsvIntegerFields =
        {
            "Year", "Mileage", "Cylinders", "Weight", "RetailPrice", "InternetPrice", "TransmissionSpeed"
        };

        public sealed class InventoryCsvValidationError
        {
            public int Row { get; set; }
            public string Field { get; set; }
            public string Value { get; set; }
            public string Message { get; set; }
        }

        private sealed class InventoryCsvValidationException : Exception
        {
            public InventoryCsvValidationException(IReadOnlyCollection<InventoryCsvValidationError> errors)
                : base($"The CSV contains {errors.Count} validation error(s). Correct the file before importing it.")
            {
                Errors = errors;
            }

            public IReadOnlyCollection<InventoryCsvValidationError> Errors { get; }
        }

        public InventoryManagementController(
            ISessionData sessionData,
            IInventoryService inventoryService,
            IVinDecoderService vinDecoderService,
            ILogService logService,
            IEmployeesService employeesService)
            : base(sessionData, inventoryService, vinDecoderService, logService, employeesService)
        {
        }

        [HttpGet]
        public ActionResult Index()
        {
            var rollbackToken = Session[InventoryRollbackTokenSessionKey] as string;
            if (string.IsNullOrWhiteSpace(rollbackToken))
            {
                rollbackToken = Guid.NewGuid().ToString("N");
                Session[InventoryRollbackTokenSessionKey] = rollbackToken;
            }
            ViewBag.InventoryRollbackToken = rollbackToken;
            ViewBag.Message = "Inventory management";
            ViewBag.Title = "Inventory management";
            ViewBag.InventoryManagementLogs = LoadInventoryManagementLogs(true);
            ViewBag.InventoryManagementVehicles = (Model.Inventory.All ?? Array.Empty<Models.GTX>())
                .Where(vehicle => vehicle != null)
                .Select(vehicle => new
                {
                    vehicle.Stock,
                    vehicle.Image
                })
                .ToArray();

            Model.Inventory.Vehicles = Model.Inventory.All;

            return View(Model);
        }

        [HttpGet]
        public JsonResult GetInventoryManagementLogs(bool includeSkipped = false)
        {
            return new JsonResult
            {
                Data = new
                {
                    success = true,
                    logs = LoadInventoryManagementLogs(includeSkipped)
                },
                JsonRequestBehavior = JsonRequestBehavior.AllowGet,
                MaxJsonLength = int.MaxValue
            };
        }

        [HttpGet]
        public JsonResult GetInventoryManagementLogVehicles(long inventoryLogId, bool includeSkipped = false)
        {
            try
            {
                var cacheKey = Constants.INVENTORY_MANAGEMENT_VEHICLES_CACHE_PREFIX +
                    inventoryLogId.ToString(CultureInfo.InvariantCulture) + ":" + includeSkipped;
                return new JsonResult
                {
                    Data = new
                    {
                        success = true,
                        vehicles = AppCache.GetOrCreate(
                            cacheKey,
                            () => InventoryService.GetInventoryManagementVehicles(inventoryLogId, includeSkipped),
                            minutes: 10) ?? Array.Empty<InventoryManagementVehicle>()
                    },
                    JsonRequestBehavior = JsonRequestBehavior.AllowGet,
                    MaxJsonLength = int.MaxValue
                };
            }
            catch (Exception ex)
            {
                Response.StatusCode = 500;
                Log(ex);
                return Json(new { success = false, message = "Unable to load inventory management records." }, JsonRequestBehavior.AllowGet);
            }
        }

        [HttpGet]
        public ActionResult Dashboard()
        {
            ViewBag.Message = "Inventory dashboard";
            ViewBag.Title = "Inventory dashboard";

            // Render only the default period. Other periods are fetched on demand by
            // the dashboard so the initial request does not repeat the full query and payload.
            var dashboards = new Dictionary<int, InventoryDashboardSummary>
            {
                [7] = LoadInventoryDashboard(7)
            };

            return View(dashboards);
        }

        [HttpGet]
        public JsonResult GetInventoryDashboard(int days = 7)
        {
            return new JsonResult
            {
                Data = new
                {
                    success = true,
                    dashboard = LoadInventoryDashboard(days)
                },
                JsonRequestBehavior = JsonRequestBehavior.AllowGet,
                MaxJsonLength = int.MaxValue
            };
        }

        [HttpGet]
        public JsonResult GetInventoryDashboardVehicleHistory(string stock)
        {
            try
            {
                return new JsonResult
                {
                    Data = new
                    {
                        success = true,
                        history = AppCache.GetOrCreate(
                            Constants.INVENTORY_MANAGEMENT_HISTORY_CACHE_PREFIX + (stock ?? string.Empty).Trim().ToUpperInvariant(),
                            () => InventoryService.GetInventoryDashboardVehicleHistory(stock),
                            minutes: 10) ?? Array.Empty<InventoryDashboardVehicleHistory>()
                    },
                    JsonRequestBehavior = JsonRequestBehavior.AllowGet,
                    MaxJsonLength = int.MaxValue
                };
            }
            catch (Exception ex)
            {
                Response.StatusCode = 500;
                Log(ex);
                return Json(new { success = false, message = "Unable to load inventory history." }, JsonRequestBehavior.AllowGet);
            }
        }

        [HttpGet]
        public ActionResult GetInventoryDashboardVehicleDetails(string stock)
        {
            var stockText = (stock ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(stockText))
            {
                Response.StatusCode = 400;
                return Content("<div class=\"alert alert-warning mb-0\">Stock number is required.</div>", "text/html");
            }

            var vehicle = FindCurrentInventoryVehicleByStock(stockText) ?? LoadInventoryVehicleSnapshotByStock(stockText);

            if (vehicle == null)
            {
                Response.StatusCode = 404;
                return Content("<div class=\"alert alert-warning mb-0\">Vehicle details are not available.</div>", "text/html");
            }

            Model.CurrentVehicle.VehicleDetails = vehicle;
            Model.CurrentVehicle.VehicleDetails.Story = vehicle.Story;
            Model.CurrentVehicle.VehicleDataOneDetails = vehicle.DataOne;
            if (Model.CurrentVehicle.VehicleDataOneDetails == null && Model.IsDataOne)
            {
                try
                {
                    Model.CurrentVehicle.VehicleDataOneDetails = GetDecodedData(stockText);
                }
                catch (Exception ex)
                {
                    Log($"Unable to load DataOne details for dashboard stock {stockText}: {ex.Message}");
                }
            }

            return PartialView("_DashboardVehicleDetails", Model);
        }

        private Models.GTX FindCurrentInventoryVehicleByStock(string stock)
        {
            var stockText = (stock ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(stockText))
            {
                return null;
            }

            return Model.Inventory.All?.FirstOrDefault(m =>
                string.Equals((m.Stock ?? string.Empty).Trim(), stockText, StringComparison.OrdinalIgnoreCase));
        }

        private Models.GTX LoadInventoryVehicleSnapshotByStock(string stock)
        {
            var snapshot = InventoryService.GetInventoryVehicleSnapshot(stock);
            var vehicle = Models.GTX.ToGTX(snapshot == null ? null : new[] { snapshot }).FirstOrDefault();
            if (vehicle == null)
            {
                return null;
            }

            HydrateInventoryVehicleMedia(vehicle, stock);

            return vehicle;
        }

        private void HydrateInventoryVehicleMedia(Models.GTX vehicle, string requestedStock)
        {
            if (vehicle == null)
            {
                return;
            }

            var stock = (vehicle.Stock ?? requestedStock ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(stock))
            {
                vehicle.Images = Array.Empty<Services.Image>();
                vehicle.Image = DefaultInventoryImage;
                return;
            }

            var images = InventoryService.GetImages(stock) ?? Array.Empty<Services.Image>();
            vehicle.Images = images;
            vehicle.Image = images.Length > 0
                ? $"{imageFolder}{BuildStockImageSource(stock, images[0].Source)}"
                : DefaultInventoryImage;
        }

        [HttpPost]
        public ActionResult PreviewInventoryUpload(HttpPostedFileBase dataCsv)
        {
            if (dataCsv == null)
            {
                Response.StatusCode = 400;
                return Json(new { success = false, message = "Upload the data CSV." });
            }

            try
            {
                var vehicles = ParseUploadedInventoryVehicles(dataCsv);
                var result = InventoryService.PreviewInventorySync(GTX.Models.GTX.ToDTOs(vehicles));

                return CreateInventoryImportJsonResult(result, "Inventory upload preview ready.");
            }
            catch (InventoryCsvValidationException ex)
            {
                Response.StatusCode = 400;
                return Json(new { success = false, message = ex.Message, validationErrors = ex.Errors });
            }
            catch (Exception ex)
            {
                Response.StatusCode = 500;
                Log(ex);
                return Json(new { success = false, message = "Inventory upload preview failed: " + ex.Message });
            }
        }

        [HttpPost]
        public ActionResult ReplaceHeaderAndConvertToXml(HttpPostedFileBase dataCsv)
        {
            if (dataCsv == null)
            {
                Response.StatusCode = 400;
                return Json(new { success = false, message = "Upload the data CSV." });
            }

            try
            {
                var vehicles = ParseUploadedInventoryVehicles(dataCsv);
                var result = InventoryService.SyncInventory(GTX.Models.GTX.ToDTOs(vehicles));
                AppCache.ClearAll();

                return CreateInventoryImportJsonResult(result, "Inventory upload completed.");
            }
            catch (InventoryCsvValidationException ex)
            {
                Response.StatusCode = 400;
                return Json(new { success = false, message = ex.Message, validationErrors = ex.Errors });
            }
            catch (Exception ex)
            {
                Response.StatusCode = 500;
                Log(ex);
                return Json(new { success = false, message = "Inventory upload failed: " + ex.Message });
            }
        }

        private InventoryManagementLog[] LoadInventoryManagementLogs(bool includeSkipped)
        {
            try
            {
                return AppCache.GetOrCreate(
                    Constants.INVENTORY_MANAGEMENT_LOGS_CACHE_PREFIX + includeSkipped,
                    () => InventoryService.GetInventoryManagementLogs(includeSkipped),
                    minutes: 10) ?? Array.Empty<InventoryManagementLog>();
            }
            catch (Exception ex)
            {
                Log(ex);
                return Array.Empty<InventoryManagementLog>();
            }
        }

        private InventoryDashboardSummary LoadInventoryDashboard(int days)
        {
            try
            {
                return AppCache.GetOrCreate(
                    Constants.INVENTORY_MANAGEMENT_DASHBOARD_CACHE_PREFIX + days.ToString(CultureInfo.InvariantCulture),
                    () => InventoryService.GetInventoryDashboard(days),
                    minutes: 5) ?? CreateEmptyInventoryDashboard(days);
            }
            catch (Exception ex)
            {
                Log(ex);
                return CreateEmptyInventoryDashboard(days);
            }
        }

        private static InventoryDashboardSummary CreateEmptyInventoryDashboard(int days)
        {
            var now = DateTime.UtcNow;
            return new InventoryDashboardSummary
            {
                Days = days,
                PeriodStartUtc = now.AddDays(-days),
                PeriodEndUtc = now,
                StatusCounts = Array.Empty<InventoryDashboardStatusCount>(),
                LocationCounts = Array.Empty<InventoryDashboardLocationCount>(),
                StatusTrend = Array.Empty<InventoryDashboardStatusTrendPoint>(),
                Vehicles = Array.Empty<InventoryDashboardVehicle>()
            };
        }

        private GTX.Models.GTX[] ParseUploadedInventoryVehicles(HttpPostedFileBase dataCsv)
        {
            byte[] csvBytes;
            using (var csvBuffer = new MemoryStream())
            {
                if (dataCsv.InputStream.CanSeek)
                {
                    dataCsv.InputStream.Position = 0;
                }
                dataCsv.InputStream.CopyTo(csvBuffer);
                csvBytes = csvBuffer.ToArray();
            }

            byte[] headerBytes;
            using (var headerStream = GetHeaderStream())
            using (var headerBuffer = new MemoryStream())
            {
                headerStream.CopyTo(headerBuffer);
                headerBytes = headerBuffer.ToArray();
            }

            var validationErrors = ValidateInventoryCsv(csvBytes, headerBytes);
            if (validationErrors.Count > 0)
            {
                throw new InventoryCsvValidationException(validationErrors);
            }

            XDocument doc;
            using (var dataStream = new MemoryStream(csvBytes, writable: false))
            using (var headerStream = new MemoryStream(headerBytes, writable: false))
            {
                doc = CsvToXmlHelper.BuildXmlFromCsv(dataStream, headerStream, new CsvXmlOptions());
            }

            GTXInventory inventory;
            var serializer = new XmlSerializer(typeof(GTXInventory));

            using (var reader = doc.CreateReader())
            {
                inventory = (GTXInventory)serializer.Deserialize(reader);
            }

            return (inventory.Vehicles ?? Array.Empty<GTX.Models.GTX>())
                .Where(m => m.SetToUpload == "Y")
                .Select(m =>
                {
                    m.Transmission = NormalizeImportedTransmissionCode(m.Transmission);
                    return m;
                })
                .ToArray();
        }

        [HttpPost]
        public ActionResult RollbackLatestInventoryUpload(long expectedLatestInventoryLogId, bool acknowledge, string rollbackToken)
        {
            var expectedRollbackToken = Session[InventoryRollbackTokenSessionKey] as string;
            if (string.IsNullOrWhiteSpace(expectedRollbackToken) ||
                !string.Equals(expectedRollbackToken, rollbackToken, StringComparison.Ordinal))
            {
                Response.StatusCode = 400;
                return Json(new { success = false, message = "The rollback request expired. Refresh the page and try again." });
            }

            if (!acknowledge)
            {
                Response.StatusCode = 400;
                return Json(new { success = false, message = "Inventory rollback confirmation is required." });
            }

            try
            {
                var result = InventoryService.RollbackLatestInventoryUpload(expectedLatestInventoryLogId);
                AppCache.ClearAll();

                return Json(new
                {
                    success = true,
                    message = "The latest inventory upload was removed and the previous inventory was restored.",
                    rollback = result
                });
            }
            catch (InvalidOperationException ex)
            {
                Response.StatusCode = 409;
                return Json(new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                Response.StatusCode = 500;
                Log(ex);
                return Json(new { success = false, message = "Unable to roll back the latest inventory upload." });
            }
        }

        private static List<InventoryCsvValidationError> ValidateInventoryCsv(byte[] csvBytes, byte[] headerBytes)
        {
            var errors = new List<InventoryCsvValidationError>();
            string[] headers;

            using (var headerStream = new MemoryStream(headerBytes, writable: false))
            using (var headerParser = CreateCsvParser(headerStream))
            {
                headers = headerParser.ReadFields() ?? Array.Empty<string>();
            }

            if (headers.Length == 0)
            {
                errors.Add(CreateCsvValidationError(1, "Header", string.Empty, "The configured import header is empty."));
                return errors;
            }

            var indexes = headers
                .Select((name, index) => new { Name = (name ?? string.Empty).Trim(), Index = index })
                .Where(item => !string.IsNullOrWhiteSpace(item.Name))
                .GroupBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First().Index, StringComparer.OrdinalIgnoreCase);

            var seenStocks = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            using (var dataStream = new MemoryStream(csvBytes, writable: false))
            using (var parser = CreateCsvParser(dataStream))
            {
                if (!parser.EndOfData)
                {
                    parser.ReadFields();
                }

                var rowNumber = 1;
                while (!parser.EndOfData)
                {
                    rowNumber++;
                    string[] fields;
                    try
                    {
                        fields = parser.ReadFields() ?? Array.Empty<string>();
                    }
                    catch (MalformedLineException ex)
                    {
                        errors.Add(CreateCsvValidationError(rowNumber, "Row", string.Empty, ex.Message));
                        continue;
                    }

                    if (!HasInventoryRowData(fields))
                    {
                        continue;
                    }

                    var setToUpload = GetCsvValue(fields, indexes, "SetToUpload").Trim();
                    if (!string.Equals(setToUpload, "Y", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (fields.Length < headers.Length)
                    {
                        errors.Add(CreateCsvValidationError(
                            rowNumber,
                            "Columns",
                            fields.Length.ToString(CultureInfo.InvariantCulture),
                            $"Expected {headers.Length} columns but found {fields.Length}."));
                    }
                    else if (fields.Skip(headers.Length).Any(value => !string.IsNullOrWhiteSpace(value)))
                    {
                        errors.Add(CreateCsvValidationError(
                            rowNumber,
                            "Columns",
                            string.Join(", ", fields.Skip(headers.Length)),
                            $"Unexpected data was found after the expected {headers.Length} columns."));
                    }

                    foreach (var rule in InventoryCsvTextLimits)
                    {
                        var value = GetCsvValue(fields, indexes, rule.Key);
                        if (value.Length > rule.Value)
                        {
                            errors.Add(CreateCsvValidationError(
                                rowNumber,
                                rule.Key,
                                value,
                                $"Maximum length is {rule.Value} character(s); found {value.Length}."));
                        }
                    }

                    var stock = GetCsvValue(fields, indexes, "Stock").Trim();
                    if (string.IsNullOrWhiteSpace(stock))
                    {
                        errors.Add(CreateCsvValidationError(rowNumber, "Stock", stock, "Stock is required for an uploaded vehicle."));
                    }
                    else
                    {
                        int firstRow;
                        if (seenStocks.TryGetValue(stock, out firstRow))
                        {
                            errors.Add(CreateCsvValidationError(rowNumber, "Stock", stock, $"Duplicate stock; it first appears on row {firstRow}."));
                        }
                        else
                        {
                            seenStocks[stock] = rowNumber;
                        }
                    }

                    foreach (var fieldName in InventoryCsvIntegerFields)
                    {
                        var value = GetCsvValue(fields, indexes, fieldName).Trim();
                        int parsed;
                        if (string.IsNullOrWhiteSpace(value) || !int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed))
                        {
                            errors.Add(CreateCsvValidationError(rowNumber, fieldName, value, "A whole number is required."));
                        }
                    }

                }
            }

            return errors;
        }

        private static TextFieldParser CreateCsvParser(Stream stream)
        {
            var parser = new TextFieldParser(stream, System.Text.Encoding.UTF8, detectEncoding: true)
            {
                TextFieldType = FieldType.Delimited,
                HasFieldsEnclosedInQuotes = true
            };
            parser.SetDelimiters(",");
            return parser;
        }

        private static bool HasInventoryRowData(string[] fields)
        {
            return fields != null &&
                ((fields.Length > 1 && !string.IsNullOrEmpty(fields[1])) ||
                 (fields.Length > 2 && !string.IsNullOrEmpty(fields[2])) ||
                 (fields.Length > 3 && !string.IsNullOrEmpty(fields[3])));
        }

        private static string GetCsvValue(string[] fields, IDictionary<string, int> indexes, string fieldName)
        {
            int index;
            return indexes.TryGetValue(fieldName, out index) && index >= 0 && index < fields.Length
                ? fields[index] ?? string.Empty
                : string.Empty;
        }

        private static string NormalizeImportedTransmissionCode(string value)
        {
            var firstLetter = (value ?? string.Empty).FirstOrDefault(char.IsLetter);
            return firstLetter == default(char)
                ? string.Empty
                : char.ToUpperInvariant(firstLetter).ToString();
        }

        private static InventoryCsvValidationError CreateCsvValidationError(int row, string field, string value, string message)
        {
            return new InventoryCsvValidationError
            {
                Row = row,
                Field = field ?? string.Empty,
                Value = value ?? string.Empty,
                Message = message ?? string.Empty
            };
        }

        private JsonResult CreateInventoryImportJsonResult(InventoryImportResult result, string message)
        {
            var inventoryDate = result.InventoryDate;
            if (inventoryDate != default(DateTime) && Model?.Environment != CommonUnit.Environment.Dev)
            {
                inventoryDate = inventoryDate.AddHours(-5);
            }

            return new JsonResult
            {
                Data = new
                {
                    success = true,
                    message = message,
                    inventoryDate = inventoryDate == default(DateTime)
                        ? null
                        : inventoryDate.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture),
                    imported = result.Imported,
                    updated = result.Updated,
                    inserted = result.Inserted,
                    removed = result.Removed,
                    skipped = result.Skipped
                },
                MaxJsonLength = int.MaxValue
            };
        }

        private Stream GetHeaderStream()
        {
            if (_cachedHeaderBytes == null)
            {
                lock (_headerLock)
                {
                    if (_cachedHeaderBytes == null)
                    {
                        var headerPath = Server.MapPath(HeaderFileVirtualPath);
                        _cachedHeaderBytes = System.IO.File.ReadAllBytes(headerPath);
                    }
                }
            }

            return new MemoryStream(_cachedHeaderBytes, writable: false);
        }
    }
}
