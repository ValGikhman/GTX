using GTX.Common;
using GTX.Models;
using Services;
using System;
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

        private static byte[] _cachedHeaderBytes;
        private static readonly object _headerLock = new object();

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
            ViewBag.Message = "Inventory management";
            ViewBag.Title = "Inventory management";
            ViewBag.InventoryManagementLogs = LoadInventoryManagementLogs(true);

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
                return new JsonResult
                {
                    Data = new
                    {
                        success = true,
                        vehicles = InventoryService.GetInventoryManagementVehicles(inventoryLogId, includeSkipped)
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
                return InventoryService.GetInventoryManagementLogs(includeSkipped) ?? Array.Empty<InventoryManagementLog>();
            }
            catch (Exception ex)
            {
                Log(ex);
                return Array.Empty<InventoryManagementLog>();
            }
        }

        private GTX.Models.GTX[] ParseUploadedInventoryVehicles(HttpPostedFileBase dataCsv)
        {
            XDocument doc;
            using (var headerStream = GetHeaderStream())
            {
                doc = CsvToXmlHelper.BuildXmlFromCsv(dataCsv.InputStream, headerStream, new CsvXmlOptions());
            }

            GTXInventory inventory;
            var serializer = new XmlSerializer(typeof(GTXInventory));

            using (var reader = doc.CreateReader())
            {
                inventory = (GTXInventory)serializer.Deserialize(reader);
            }

            return (inventory.Vehicles ?? Array.Empty<GTX.Models.GTX>())
                .Where(m => m.SetToUpload == "Y")
                .ToArray();
        }

        private JsonResult CreateInventoryImportJsonResult(InventoryImportResult result, string message)
        {
            var inventoryDate = result.InventoryDate;
            if (inventoryDate != default(DateTime) && !(Model?.IsDevelopment ?? false))
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
