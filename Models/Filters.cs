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
        public string[] DriveTrains { get; set; }
        public string[] BodyTypes { get; set; }
        public int MaxMilege { get; set; }
        public int MinMilege { get; set; }
        public int Milege { get; set; }

        public int MaxPrice { get; set; }
        public int MinPrice { get; set; }
        public int Price { get; set; }

    }
}