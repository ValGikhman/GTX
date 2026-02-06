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
        public VinDecoderController(ISessionData sessionData, IInventoryService inventoryService, IVinDecoderService vinDecoderService, IEZ360Service _ez360Service, ILogService logService, IEmployeesService employeesService)
        : base(sessionData, inventoryService, vinDecoderService, _ez360Service, logService, employeesService) {}
        // GET: VinDecoder
        public ActionResult Index()
        {
            return View();
        }

        [HttpGet]
        public string DecodeDataOneByAnyVin(string vin)
        {
            try
            {
                var details = VinDecoderService.DecodeVin(vin, dataOneApiKey, dataOneSecretApiKey);
                var vehicle = Model.Inventory.All?.FirstOrDefault(m => m.VIN == vin);
                Model.CurrentVehicle.VehicleDetails = vehicle;
                Model.CurrentVehicle.VehicleDataOneDetails = Models.GTX.SetDecodedData(details);
                var res = RenderViewToString(ControllerContext, "_DetailsDataOne", Model);
                return res;
            }
            catch (Exception ex)
            {
                return "Error: " + ex.Message;
            }
        }
    }
}