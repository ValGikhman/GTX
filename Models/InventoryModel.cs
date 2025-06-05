using System.Collections.Generic;
using System.ComponentModel;
using System.Web.Mvc;
using GTX.Common;

namespace GTX.Models {
    public class InventoryModel {

        [DisplayName("Inventory")]
        public IEnumerable<Vehicle> Vehicles { get; set; }
    }
}