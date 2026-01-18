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

            if (HttpContext.Current.Session[Constants.SESSION_INVENTORY] != null) {
                Inventory = (Inventory)HttpContext.Current.Session[Constants.SESSION_INVENTORY];
            }

            if (HttpContext.Current.Session[Constants.SESSION_EMPLOYERS] != null) {
                Employers = (Employer[])HttpContext.Current.Session[Constants.SESSION_EMPLOYERS];
            }

            if (HttpContext.Current.Session[Constants.SESSION_CURRENT_FILTER] != null) {
                CurrentFilter = (Filters)HttpContext.Current.Session[Constants.SESSION_CURRENT_FILTER];
            }

            if (HttpContext.Current.Session[Constants.SESSION_OPEN_HOURS] != null) {
                OpenHours = (OpenHours[])HttpContext.Current.Session[Constants.SESSION_OPEN_HOURS];
            }
        }

        #endregion Public Constructors

        public bool IsMajordome { get; set; }

        public bool IsEZ360 { get; set; }

        public bool IsDataOne { get; set; }

        public bool IsDevelopment { get; set; }

        public Inventory Inventory { get; set; }

        public Employer[] Employers { get; set; }

        public Filters CurrentFilter { get; set; }

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