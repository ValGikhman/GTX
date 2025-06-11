using System;
using System.Collections.Generic;
using System.Xml.Serialization;


namespace GTX.Models {
	[XmlRoot(ElementName = "GTX-Inventory")]
	public class GTXInventory {

		[XmlElement(ElementName = "vehicle")]
		public Vehicle[] Vehicles { get; set; }
	}


	[XmlRoot(ElementName = "vehicle")]
	public class Vehicle {
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

		[XmlElement(ElementName = "New-used")]
		public string Newused { get; set; }

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

		[XmlElement(ElementName = "FuelType")]
		public string FuelType { get; set; }

		[XmlElement(ElementName = "ReadyToSellDate")]
		public string ReadyToSellDate { get; set; }

		[XmlElement(ElementName = "TransmissioSpeed")]
		public int TransmissioSpeed { get; set; }

		[XmlElement(ElementName = "DoNo")]
		public object DoNo { get; set; }
	}
}
