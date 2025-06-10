using GTX.Models;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Mvc;

namespace GTX.Controllers {

    public class InventoryController : BaseController {

        public async Task<ActionResult> All() {
            ViewBag.Message = "Inventory";

            InventoryModel model = new InventoryModel();
            model.Inventory = await Utility.XMLHelpers.XmlRepository.GetInventory();
            model.Inventory.Vehicles = model.Inventory.Vehicles.Where(m => m.RetailPrice > 0).ToArray();
            ViewBag.Title = $"Inventory ({model.Inventory.Vehicles.Count()}) vehicles";
            return View(model.Inventory);
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