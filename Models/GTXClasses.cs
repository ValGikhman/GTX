namespace GTX.Models {
    public class Employer {
        public int id { get; set; }

        public string Name { get; set; }

        public string Position { get; set; }

        public string Phone { get; set; }

        public string Email { get; set; }
    }

    public class OpenHours {
        public string Day { get; set; }

        public int From { get; set; }

        public int To { get; set; }

        public string Description { get; set; }
    }

    public class Inventory {
        public string Title { get; set; }

        public GTX[] Vehicles { get; set; }

        public GTX[] All { get; set; }

        public GTX[] Suvs { get; set; }

        public GTX[] Cars { get; set; }

        public GTX[] Trucks { get; set; }

        public GTX[] Vans { get; set; }

        public GTX[] Cargo { get; set; }

        public GTX[] Convertibles { get; set; }

        public GTX[] Coupe { get; set; }

        public GTX[] Hatchbacks { get; set; }

    }

    public class Filters {
        public string[] Makes { get; set; }

        public string[] Models { get; set; }

        public string[] Engines { get; set; }

        public string[] FuelTypes { get; set; }

        public string[] DriveTrains { get; set; }

        public string[] BodyTypes { get; set; }

        public string[] VehicleTypes { get; set; }

        public int MaxMilege { get; set; }

        public int MinMilege { get; set; }

        public int MaxPrice { get; set; }

        public int MinPrice { get; set; }
    }
}