using GTX.Models;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Hosting;
using System.Xml.Linq;
using System.Xml.Serialization;

namespace Utility.XMLHelpers {
    public static class XmlRepository {
        private static string xmlFilePath = HostingEnvironment.MapPath("~/App_Data/");

        public async static Task<Employer[]> GetEmployers() {
            string path = $"{xmlFilePath}GTX-Staff.xml";

            XDocument doc = XDocument.Load(path);
            return doc.Descendants("employer") .Select(x => new Employer {
                    id = int.Parse(x.Element("id").Value),
                    Name = x.Element("name").Value,
                    Phone = x.Element("phone").Value,
                    Position = x.Element("position").Value,
                    Email = x.Element("email").Value
                })
                .OrderBy(m => m.id)
                .ToArray();
        }

        public static OpenHours[] GetOpenHours() {
            string path = $"{xmlFilePath}GTX-Open.xml";

            XDocument doc = XDocument.Load(path);
            return
                doc.Descendants("open").Select(x => new OpenHours {
                    Day = x.Element("day").Value,
                    From = int.TryParse(x.Element("from")?.Value, out int fromVal) ? fromVal : 0,
                    To = int.TryParse(x.Element("to")?.Value, out int toVal) ? toVal : 0,
                    Description = x.Element("description").Value
                })
                .ToArray();
        }

        public async static Task<GTX.Models.GTX[]> GetInventory() {
            string path = $"{xmlFilePath}\\Inventory\\Current\\GTX-Inventory.xml";
            GTXInventory inventory = await ReadXmlFile(path);
            inventory.Vehicles = inventory.Vehicles.Where(m => m.RetailPrice > 0 && !string.IsNullOrEmpty(m.VIN)).ToArray();

            return inventory.Vehicles;
        }

        public async static Task<GTXInventory> ReadXmlFile(string filePath) {
            XmlSerializer serializer = new XmlSerializer(typeof(GTXInventory));

            using (StreamReader reader = new StreamReader(filePath)) {
                GTXInventory inventory = (GTXInventory)serializer.Deserialize(reader);
                return inventory;
            }
        }
    }
}