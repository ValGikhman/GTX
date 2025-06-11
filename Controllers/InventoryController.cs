using GTX.Models;
using GTX.Session;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Mvc;

namespace GTX.Controllers {

    public class InventoryController : BaseController {

        public InventoryController(ISessionData sessionData)
            : base(sessionData) {}

        public async Task<ActionResult> All() {
            ViewBag.Message = "Inventory";

            Vehicle[] model = await SetModel();

            ViewBag.Title = $"Inventory ({model.Length}) vehicles";
            return View(model);
        }


        private async Task<Vehicle[]> SetModel() {
            if (SessionData?.Vehicles == null) {
                Vehicle[] model;
                model = await Utility.XMLHelpers.XmlRepository.GetInventory();
                model = model.Where(m => m.RetailPrice > 0).ToArray();
                SessionData.SetSession(Constants.SESSION_INVENTORY, model);
                return model;
            }

            return SessionData.Vehicles;
        }

        private async Task<string> DecodeVin(string vin) {
            using (HttpClient client = new HttpClient()) {
                string url = $"https://vpic.nhtsa.dot.gov/api/vehicles/decodevin/{vin}?format=xml";
                HttpResponseMessage response = await client.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                    return "Error fetching VIN data.";

                var data = await response.Content.ReadAsStringAsync();
                return data;
            }
        }
    }
}