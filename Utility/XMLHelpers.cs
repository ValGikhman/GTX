using GTX.Models;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Hosting;
using System.Xml.Linq;
using System.Xml.Serialization;

namespace Utility.XMLHelpers {
   public static class XmlRepository {
        private static string xmlFilePath = HostingEnvironment.MapPath("~/App_Data/");

        public static IList<Employer> GetEmployers() {
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
                .ToList();
        }

        public static IList<OpenHours> GetOpenHours() {
            string path = $"{xmlFilePath}GTX-Open.xml";

            XDocument doc = XDocument.Load(path);
            return
                doc.Descendants("open").Select(x => new OpenHours {
                    Day = x.Element("day").Value,
                    From = x.Element("from").Value,
                    To = x.Element("to").Value,
                    Description = x.Element("description").Value
                })
                .ToList();
        }

        public async static Task<GTXInventory> GetInventory() {
            string path = $"{xmlFilePath}\\Inventory\\Current\\GTX-Inventory.xml";
            GTXInventory inventory = ReadXmlFile(path);
            inventory.Vehicles = inventory.Vehicles.Where(m => m.RetailPrice > 0 && !string.IsNullOrEmpty(m.VIN)).ToArray();

            return inventory;
        }

        public static GTXInventory ReadXmlFile(string filePath) {
            XmlSerializer serializer = new XmlSerializer(typeof(GTXInventory));

            using (StreamReader reader = new StreamReader(filePath)) {

                GTXInventory inventory = (GTXInventory)serializer.Deserialize(reader);
                return inventory;
            }
        }
    }
}