using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace GTX.Models {
    public class StaffModel: BaseModel {

        public Employer[] Employers { get; set; }
    }
}