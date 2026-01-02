using Common;
using Services;
using System;
using System.Collections.Generic;
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
	}
}
