using Services;
using System;
using System.Linq;
using System.Xml.Serialization;

namespace GTX.Models {
	[XmlRoot(ElementName = "GTX-Inventory")]
	public class GTXInventory {

		[XmlElement(ElementName = "vehicle")]
		public GTX[] Vehicles { get; set; }
	}


	[XmlRoot(ElementName = "vehicle")]
	public class GTX {
		[XmlElement(ElementName = "Stock")]
		public string Stock { get; set; }

		[XmlElement(ElementName = "Year")]
		public int Year { get; set; }

		[XmlElement(ElementName = "Make")]
		public string Make { get; set; }

		[XmlElement(ElementName = "Model")]
		public string Model { get; set; }

		[XmlElement(ElementName = "VIN")]
		public string VIN { get; set; }

		[XmlElement(ElementName = "Mileage")]
		public int Mileage { get; set; }

		[XmlElement(ElementName = "Cylinders")]
		public int Cylinders { get; set; }

		[XmlElement(ElementName = "Weight")]
		public int Weight { get; set; }

		[XmlElement(ElementName = "Color")]
		public string Color { get; set; }

		[XmlElement(ElementName = "Color2")]
		public string Color2 { get; set; }

		[XmlElement(ElementName = "Features")]
		public string Features { get; set; }

		[XmlElement(ElementName = "RetailPrice")]
		public int RetailPrice { get; set; }

		[XmlElement(ElementName = "InternetPrice")]
		public int InternetPrice { get; set; }

		[XmlElement(ElementName = "DriveTrain")]
		public string DriveTrain { get; set; }

		[XmlElement(ElementName = "LocationCode")]
		public string LocationCode { get; set; }

		[XmlElement(ElementName = "Body")]
		public string Body { get; set; }

		[XmlElement(ElementName = "Engine")]
		public string Engine { get; set; }

		[XmlElement(ElementName = "Transmission")]
		public string Transmission { get; set; }

		[XmlElement(ElementName = "PurchaseDate")]
		public string PurchaseDate { get; set; }

		[XmlElement(ElementName = "ArrivalDate")]
		public string ArrivalDate { get; set; }

		[XmlElement(ElementName = "FuelType")]
		public string FuelType { get; set; }

		[XmlElement(ElementName = "TransmissionSpeed")]
		public int TransmissionSpeed { get; set; }

		[XmlElement(ElementName = "VehicleType")]
		public string VehicleType { get; set; }

		[XmlElement(ElementName = "VehicleStyle")]
		public string VehicleStyle { get; set; }

		[XmlElement(ElementName = "SetToUpload")]
		public string SetToUpload { get; set; }

		public Image[] Images { get; set; }

		public string Image { get; set; }

		public Story Story { get; set; }

		public DecodedData DataOne { get; set; }

		public string TransmissionWord { get; set; }
    }


    public static class VehicleMapper
        {
            public static GTX[] ToGTXInventory(EZ360.Vehicle[] vehicles)
            {
                if (vehicles == null || vehicles.Length == 0)
                    return Array.Empty<GTX>();

                var mapped = vehicles.Select(v => new GTX
                {
                    Stock = v.StockNo,
                    Year = v.Year,
                    Make = v.Make.ToUpper(),
                    Model = v.Model.ToUpper(),
                    VIN = v.Vin,
                    Mileage = TryParseInt(v.Miles),
                    Cylinders = ExtractCylinders(v.Engine),
                    Weight = 0, // no equivalent property — fill with default
                    Color = v.ExtColor,
                    Color2 = v.IntColor,
                    Features = v.Options,
                    RetailPrice = v.PriceWeb,
                    InternetPrice = v.PriceWeb,
                    DriveTrain = v.Drivetrain,
                    LocationCode = v.ProjectId,
                    Body = v.Body.ToUpper(),
                    Engine = v.Engine,
                    Transmission = ExtractTransmissionType(v.Transmission),
                    PurchaseDate = v.CreatedOn.ToString("yyyy-MM-dd"),
                    ArrivalDate = v.CreatedOn.ToString("yyyy-MM-dd"),
                    FuelType = v.FuelType,
                    TransmissionSpeed = ExtractTransmissionSpeed(v.Transmission),
                    VehicleType = v.Body.ToUpper(),
                    VehicleStyle = v.Trim.ToUpper(),
                    SetToUpload = v.Active ? "Y" : "N",
                    Image = v.ThirdPartyPics.FirstOrDefault(),
                    Images = v.ThirdPartyPics.Select(m => new Image() { Id=Guid.Empty, Stock=v.StockNo, DateCreated=DateTime.Now, Order=0, Source=m}).ToArray(),
                    TransmissionWord = v.Transmission,
                    Story = null,
                    DataOne = null
                }).Where(m => m.Mileage > 0).OrderBy(m => m.Make).ToArray();


                return mapped;
            }

            private static int TryParseInt(string value)
            {
                if (int.TryParse(value?.Replace(",", ""), out int result))
                    return result;
                return 0;
            }

            private static int ExtractCylinders(string engine)
            {
                // crude example: looks for "V6" or "4CYL"
                if (string.IsNullOrEmpty(engine)) return 0;
                if (engine.ToUpper().Contains("V6")) return 6;
                if (engine.ToUpper().Contains("V8")) return 8;
                if (engine.Contains("4")) return 4;
                return 0;
            }

            private static int ExtractTransmissionSpeed(string transmission)
            {
                // crude parser for "6-Speed Automatic" or "8-Spd"
                if (string.IsNullOrEmpty(transmission)) return 0;
                var parts = transmission.Split(' ', '-', (char)StringSplitOptions.RemoveEmptyEntries);
                foreach (var part in parts)
                    if (int.TryParse(part.Replace("Spd", ""), out int speed))
                        return speed;
                return 0;
            }

            private static string ExtractTransmissionType(string transmission)
            {
                if (string.IsNullOrEmpty(transmission)) return "A";
                return transmission.Trim().Substring(0,1);
            }
    }
}
