using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace GTX.Models {
    public class BaseModel {
        #region Public Constructors

        public BaseModel() {

            if (HttpContext.Current.Session[Constants.SESSION_INVENTORY] != null) {
                Vehicles = (Vehicle[])HttpContext.Current.Session[Constants.SESSION_INVENTORY];
            }

            if (HttpContext.Current.Session[Constants.SESSION_EMPLOYERS] != null) {
                Employers = (Employer[])HttpContext.Current.Session[Constants.SESSION_EMPLOYERS];
            }

            if (HttpContext.Current.Session[Constants.SESSION_EMPLOYERS] != null) {
                Makes = (string[])HttpContext.Current.Session[Constants.SESSION_MAKES];
            }
        }

        #endregion Public Constructors

        public Vehicle[] Vehicles { get; set; }

        public Employer[] Employers { get; set; }

        public string[] Makes { get; set; }
    }
}