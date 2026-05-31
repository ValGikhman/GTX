using GTX.Common;
using Services;
using System;
using System.Linq;
using System.Web.Mvc;

namespace GTX.Controllers
{
    [RequireAdminRole]
    public class VinDecoderController : BaseController
    {
        public VinDecoderController(ISessionData sessionData, IInventoryService inventoryService, IVinDecoderService vinDecoderService, ILogService logService, IEmployeesService employeesService)
        : base(sessionData, inventoryService, vinDecoderService, logService, employeesService) {}
        // GET: VinDecoder
        public ActionResult Index()
        {
            return View();
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
    }
}
