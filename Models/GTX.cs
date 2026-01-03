using Common;
using Services;
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Xml.Linq;
using System.Xml.Serialization;

namespace GTX.Models
{
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

		public EZ360.Vehicle EZ360 { get; set; }

		public string TransmissionWord { get; set; }

		public static GTXDTO ToDTO(GTX g)
		{
			if (g == null) return null;

			return new GTXDTO
			{
				Stock = g.Stock,
				Year = g.Year,
				Make = g.Make,
				Model = g.Model,
				VIN = g.VIN,
				Mileage = g.Mileage,
				Cylinders = g.Cylinders,
				Weight = g.Weight,
				Color = g.Color,
				Color2 = g.Color2,
				Features = g.Features,
				RetailPrice = g.RetailPrice,
				InternetPrice = g.InternetPrice,
				DriveTrain = g.DriveTrain,
				LocationCode = g.LocationCode,
				Body = g.Body,
				Engine = g.Engine,
				Transmission = g.Transmission,
				PurchaseDate = g.PurchaseDate,
				ArrivalDate = g.ArrivalDate,
				FuelType = g.FuelType,
				TransmissionSpeed = g.TransmissionSpeed,
				VehicleType = g.VehicleType,
				VehicleStyle = g.VehicleStyle,
				SetToUpload = g.SetToUpload
			};
		}

		public static GTXDTO[] ToDTOs(GTX[] vehicles)
		{
			if (vehicles == null) return Array.Empty<GTXDTO>();

			return vehicles.Select(ToDTO).ToArray();
		}

		public static Models.GTX[] ToGTX(GTXDTO[] source)
		{
			if (source == null)	return Array.Empty<Models.GTX>();
			var options = new JsonSerializerOptions
			{
				PropertyNameCaseInsensitive = true
			};

			return source.Select(m => new Models.GTX
				{
					Stock = m.Stock,
					Year = m.Year,
					Make = m.Make,
					Model = m.Model,
					VIN = m.VIN,
					Mileage = m.Mileage,
					Cylinders = m.Cylinders,
					Weight = m.Weight,
					Color = m.Color,
					Color2 = m.Color2,
					Features = m.Features,
					RetailPrice = m.RetailPrice,
					InternetPrice = m.InternetPrice,
					DriveTrain = m.DriveTrain,
					LocationCode = m.LocationCode,
					Body = m.Body,
					Engine = m.Engine,
					Transmission = m.Transmission,
					PurchaseDate = m.PurchaseDate,
					ArrivalDate = m.ArrivalDate,
					FuelType = m.FuelType,
					TransmissionSpeed = m.TransmissionSpeed,
					TransmissionWord = WordIt(m.Transmission),
					VehicleType = m.VehicleType,
					VehicleStyle = m.VehicleStyle,
					SetToUpload = m.SetToUpload,
					Story = m.Story == null ? null : new Story { Id = m.Story.Id, HtmlContent = m.Story.HtmlContent, Title = m.Story.Title }, 
					DataOne = m.DataOne == null ? null : SetDecodedData(m.DataOne.DataOneContent),
					EZ360 = m.EZ360 == null ? null : JsonSerializer.Deserialize<EZ360.Vehicle>(m.EZ360.EZ360, options)
			}).ToArray();
		}

		public static string WordIt(string? transmission)
		{
			try
			{
				string res = string.Empty;
				if (transmission != null)
				{
					switch (transmission)
					{
						case "A":
							return "Automatic";

						case "M":
							return "Manual";

						case "T":
							return "Transverse";

						case "C":
							return "Continuously variable";

						default:
							return transmission;
					}
				}

				return transmission;
			}
			catch
			{
				return "N/A";
			}
		}

		public static DecodedData SetDecodedData(string dataOne)
		{
			var (errCode, errMsg) = ParseDecoderError(dataOne);

			if (errCode != null && errCode != "RI")
			{
				Console.WriteLine(errMsg);
				return null;
			}

			try
			{
				var serializer = new XmlSerializer(typeof(DecodedData));
				using (TextReader reader = new StringReader(dataOne))
				{
					return (DecodedData)serializer.Deserialize(reader);
				}
			}
			catch (Exception ex)
			{
				return null;
			}
		}

		public static (string code, string? message) ParseDecoderError(string xml)
		{
			try
			{
				var doc = System.Xml.Linq.XDocument.Parse(xml);
				var err = doc.Descendants("decoder_errors").Descendants("error").FirstOrDefault();
				if (err == null) return (null, null);

				var code = (string?)err.Element("code");
				var msg = (string?)err.Element("message");

				if (code == "RI") return (null, null);

				return (code, msg);
			}
			catch
			{
				// If it isn't valid XML, treat as a body/format error
				return ("PARSE", "Invalid XML from decoder");
			}
		}
	}
}
