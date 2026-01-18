using Services;
using System.Collections.Generic;
using System.Web;

namespace GTX.Models
{
    public class BaseModel {
        #region Public Constructors

        public BaseModel() {
            Inventory = new Inventory();
            CurrentVehicle = new Vehicle();
        }

        #endregion Public Constructors

        public Dictionary<string, GTX[]> Categories { get; set; }

        public bool IsMajordome { get; set; }

        public bool IsEZ360 { get; set; }

        public bool IsDataOne { get; set; }

        public bool IsDevelopment { get; set; }

        public Inventory Inventory { get; set; }

        public Employer[] Employers { get; set; }

        public Filters Filters { get; set; }

        public Vehicle CurrentVehicle { get; set; }

        public OpenHours[] OpenHours { get; set; }

        public List<BlogPostModel> Blogs { get; set; }
    }

    public class Vehicle { 
        public GTX VehicleDetails { get; set; }

        public DecodedData VehicleDataOneDetails { get; set; }

        public bool DisplayEZ360Player { get; set; }

        public GTX[] VehicleSuggesion { get; set; }
    }
}