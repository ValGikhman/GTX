using System.Web;

namespace GTX.Models {
    public class BaseModel {
        #region Public Constructors

        public BaseModel() {

            Inventory = new Inventory();
            CurrentFilter = new Filters();

            if (HttpContext.Current.Session[Constants.SESSION_INVENTORY] != null) {
                Inventory = (Inventory)HttpContext.Current.Session[Constants.SESSION_INVENTORY];
            }

            if (HttpContext.Current.Session[Constants.SESSION_EMPLOYERS] != null) {
                Employers = (Employer[])HttpContext.Current.Session[Constants.SESSION_EMPLOYERS];
            }

            if (HttpContext.Current.Session[Constants.SESSION_CURRENT_FILTER] != null) {
                CurrentFilter = (Filters)HttpContext.Current.Session[Constants.SESSION_CURRENT_FILTER];
            }


            if (HttpContext.Current.Session[Constants.SESSION_OOPEN_HOURS] != null) {
                OpenHours = (OpenHours[])HttpContext.Current.Session[Constants.SESSION_OOPEN_HOURS];
            }
        }

        #endregion Public Constructors

        public Inventory Inventory { get; set; }

        public Employer[] Employers { get; set; }

        public Filters CurrentFilter { get; set; }

        public Models.GTX CurrentVehicle { get; set; }

        public OpenHours[] OpenHours { get; set; }
    }
}