using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace GTX.Models {
    public class Filters {
        public string[] Makes { get; set; }
        public string[] Models { get; set; }
        public string[] Engines { get; set; }
        public string[] FuelTypes { get; set; }
    }
}