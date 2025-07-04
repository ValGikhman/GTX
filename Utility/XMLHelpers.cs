﻿using GTX.Models;
using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web.Hosting;
using System.Web.Mvc;
using System.Xml;
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

        public async static Task SendAdfLeadAsync(ContactUs model, Vehicle vehicle) {
            try {
                string filePath = $"{xmlFilePath}adf.xml";

                // Load XML
                XmlDocument xmlDoc = new XmlDocument();
                xmlDoc.Load(filePath);

                // Update XML Nodes (Example)
                xmlDoc.SelectSingleNode("//requestdate")!.InnerText = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:sszzz");

                xmlDoc.SelectSingleNode("//name[@part='first']")!.InnerText = model.FirstName;
                xmlDoc.SelectSingleNode("//name[@part='last']")!.InnerText = model.LastName;
                xmlDoc.SelectSingleNode("//email")!.InnerText = model.Email;
                xmlDoc.SelectSingleNode("//phone")!.InnerText = model.Phone;

                if (vehicle.VehicleDetails != null) {
                    // Set vehicle info
                    xmlDoc.SelectSingleNode("//prospect/vehicle/stock")!.InnerText = vehicle.VehicleDetails.Stock;
                    xmlDoc.SelectSingleNode("//prospect/vehicle/year")!.InnerText = vehicle.VehicleDetails.Year.ToString();
                    xmlDoc.SelectSingleNode("//prospect/vehicle/make")!.InnerText = vehicle.VehicleDetails.Make;
                    xmlDoc.SelectSingleNode("//prospect/vehicle/model")!.InnerText = vehicle.VehicleDetails.Model;
                }

                // Set customer contact info
                xmlDoc.SelectSingleNode("//prospect/customer/contact/name[@part='first']")!.InnerText = model.FirstName; ;
                xmlDoc.SelectSingleNode("//prospect/customer/contact/name[@part='last']")!.InnerText = model.LastName; ;
                xmlDoc.SelectSingleNode("//prospect/customer/contact/email")!.InnerText = model.Email;
                xmlDoc.SelectSingleNode("//prospect/customer/contact/phone")!.InnerText = model.Phone;

                // Set comments
                xmlDoc.SelectSingleNode("//prospect/customer/comments")!.InnerText = model.Comment;

                // Convert to string
                string xmlString;
                using (var stringWriter = new StringWriter()) {
                    xmlDoc.Save(stringWriter);
                    xmlString = stringWriter.ToString();
                }

                // Send to AutoRaptor
                var url = "https://ar.autoraptor.com/incoming/adf/ARAP2237-GB";

                using (var client = new HttpClient()) {
                    var content = new StringContent(xmlString, Encoding.UTF8, "application/xml");
                    var response = await client.PostAsync(url, content);

                    if (response.IsSuccessStatusCode) {
                        //return Json(new { success = true, message = "Lead sent successfully!" });
                    }
                    else {
                        var error = await response.Content.ReadAsStringAsync();
                        //return Json(new { success = false, message = $"Failed to send. {error}" });
                    }       
                }
            }
            catch (Exception ex) {
            }
        }
    }
}