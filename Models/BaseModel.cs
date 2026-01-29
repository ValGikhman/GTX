using Services;
using System.Collections.Generic;
using System.Web;

namespace GTX.Models
{
    public class BaseModel {
        #region Public Constructors

        public BaseModel() {

            Inventory = new Inventory();
            CurrentFilter = new Filters();
            CurrentVehicle = new Vehicle();
        }

        #endregion Public Constructors

        public bool IsMajordome { get; set; }

        public bool IsEZ360 { get; set; }

        public bool IsDataOne { get; set; }

        public bool IsDevelopment { get; set; }

        public Inventory Inventory { get; set; }

        public EmployeeModel[] Employers { get; set; }

        public Filters CurrentFilter { get; set; }

        public Filters Filters { get; set; }

        public Dictionary<string, Models.GTX[]> Categories { get; set; }

        public Vehicle CurrentVehicle { get; set; }

        public OpenHours[] OpenHours { get; set; }

        public List<AnnouncementModel> Announcements { get; set; }

        public List<SitePassword> Passwords { get; set; }

        public CommonUnit.Roles  CurrentRole { get; set; }
    }

    public class Vehicle { 
        public GTX VehicleDetails { get; set; }

        public DecodedData VehicleDataOneDetails { get; set; }

        public bool DisplayEZ360Player { get; set; }

        public GTX[] VehicleSuggesion { get; set; }
    }

    public class SitePassword
    {
        public string Role { get; set; }
        public string Password { get; set; }
    }

}