using System;

namespace GTX.Models {
    public sealed class MobileInventoryQuery {
        public string Search { get; set; }
        public string Type { get; set; }
        public string Make { get; set; }
        public string Model { get; set; }
        public int? MinYear { get; set; }
        public int? MaxYear { get; set; }
        public int? MinMileage { get; set; }
        public int? MaxMileage { get; set; }
        public int? MinPrice { get; set; }
        public int? MaxPrice { get; set; }
        public string Sort { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }

    public sealed class MobileInventoryListResponse {
        public DateTime Published { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
        public int TotalPages { get; set; }
        public int DocumentaryFee { get; set; }
        public MobileVehicleDto[] Vehicles { get; set; }
    }

    public sealed class MobileVehicleDto {
        public string Stock { get; set; }
        public string Vin { get; set; }
        public int Year { get; set; }
        public string Make { get; set; }
        public string Model { get; set; }
        public string Trim { get; set; }
        public string Type { get; set; }
        public string Body { get; set; }
        public int Mileage { get; set; }
        public int Cylinders { get; set; }
        public int RetailPrice { get; set; }
        public int InternetPrice { get; set; }
        public int DocumentaryFee { get; set; }
        public int TransparentPrice { get; set; }
        public string ExteriorColor { get; set; }
        public string InteriorColor { get; set; }
        public string Drivetrain { get; set; }
        public string Engine { get; set; }
        public string Transmission { get; set; }
        public string Fuel { get; set; }
        public string LocationCode { get; set; }
        public string PrimaryImageUrl { get; set; }
        public string[] ImageUrls { get; set; }
        public string[] Features { get; set; }
        public bool HasStory { get; set; }
        public string StoryTitle { get; set; }
        public string StoryHtml { get; set; }
        public long DetailsViews { get; set; }
        public string WebsiteUrl { get; set; }
        public string CarfaxUrl { get; set; }
    }

    public sealed class MobileInventoryFiltersResponse {
        public DateTime Published { get; set; }
        public int TotalCount { get; set; }
        public string[] Makes { get; set; }
        public string[] Models { get; set; }
        public string[] VehicleTypes { get; set; }
        public string[] BodyTypes { get; set; }
        public string[] FuelTypes { get; set; }
        public string[] Drivetrains { get; set; }
        public string[] Transmissions { get; set; }
        public int MinYear { get; set; }
        public int MaxYear { get; set; }
        public int MinMileage { get; set; }
        public int MaxMileage { get; set; }
        public int MinPrice { get; set; }
        public int MaxPrice { get; set; }
    }

    public sealed class MobileAppConfigResponse {
        public string ApiVersion { get; set; }
        public string DealerName { get; set; }
        public string Phone { get; set; }
        public string Address { get; set; }
        public string WebsiteBaseUrl { get; set; }
        public string DirectionsUrl { get; set; }
        public string CreditApplicationUrl { get; set; }
        public int DocumentaryFee { get; set; }
        public MobileStoreHoursDto[] Hours { get; set; }
    }

    public sealed class MobileStoreHoursDto {
        public string Day { get; set; }
        public string Description { get; set; }
    }
}
