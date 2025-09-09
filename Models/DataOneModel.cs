using System.Collections.Generic;
using System.Xml;
using System.Xml.Serialization;

namespace GTX.Models {
    #region Root

    [XmlRoot("decoded_data")]
    public class DecodedData {
        [XmlElement("decoder_messages")]
        public DecoderMessages DecoderMessages { get; set; }

        [XmlElement("query_responses")]
        public QueryResponses QueryResponses { get; set; }
    }

    public class DecoderMessages {
        [XmlElement("service_provider")]
        public string ServiceProvider { get; set; }

        [XmlElement("decoder_version")]
        public string DecoderVersion { get; set; }

        // Often empty element
        [XmlElement("decoder_errors")]
        public string DecoderErrors { get; set; }
    }

    public class QueryResponses {
        [XmlElement("query_response")]
        public List<QueryResponse> Items { get; set; } = new();
    }

    public class QueryResponse {
        [XmlAttribute("identifier")]
        public string Identifier { get; set; }

        [XmlAttribute("transaction_id")]
        public string TransactionId { get; set; }

        [XmlElement("query_error")]
        public QueryError QueryError { get; set; }

        [XmlElement("us_market_data")]
        public UsMarketData UsMarketData { get; set; }

        // Present in your sample after us_market_data
        [XmlElement("supplemental_data")]
        public SupplementalData SupplementalData { get; set; }
    }

    public class QueryError {
        [XmlElement("error_code")]
        public string ErrorCode { get; set; }

        [XmlElement("error_message")]
        public string ErrorMessage { get; set; }
    }

    #endregion

    #region US Market Data

    public class UsMarketData {
        [XmlElement("us_styles")]
        public UsStyles UsStyles { get; set; }
    }

    public class UsStyles {
        [XmlAttribute("count")]
        public int? Count { get; set; }

        [XmlElement("style")]
        public List<Style> Styles { get; set; } = new();
    }

    public class Style {
        [XmlAttribute("name")]
        public string Name { get; set; }

        [XmlAttribute("vehicle_id")]
        public long VehicleId { get; set; }

        [XmlAttribute("complete")]
        public string Complete { get; set; }

        [XmlAttribute("market")]
        public string Market { get; set; }

        [XmlAttribute("fleet")]
        public string Fleet { get; set; }

        [XmlElement("basic_data")]
        public BasicData BasicData { get; set; }

        [XmlElement("pricing")]
        public Pricing Pricing { get; set; }

        [XmlElement("engines")]
        public Engines Engines { get; set; }

        [XmlElement("transmissions")]
        public Transmissions Transmissions { get; set; }

        [XmlElement("standard_generic_equipment")]
        public EquipmentSection StandardGenericEquipment { get; set; }

        [XmlElement("optional_generic_equipment")]
        public EquipmentSection OptionalGenericEquipment { get; set; }

        // Colors weren’t expanded in your sample. Keep a flexible placeholder.
        [XmlElement("colors")]
        public Colors Colors { get; set; }

        [XmlElement("warranties")]
        public Warranties Warranties { get; set; }

        [XmlElement("epa_fuel_efficiency")]
        public EpaFuelEfficiency EpaFuelEfficiency { get; set; }

        [XmlElement("epa_green_scores")]
        public EpaGreenScores EpaGreenScores { get; set; }

        [XmlElement("nhtsa_crash_test_ratings")]
        public NhtsaCrashTestRatings NhtsaCrashTestRatings { get; set; }
    }

    public class BasicData {
        [XmlElement("market")] public string Market { get; set; }
        [XmlElement("year")] public int Year { get; set; }
        [XmlElement("make")] public string Make { get; set; }
        [XmlElement("model")] public string Model { get; set; }
        [XmlElement("trim")] public string Trim { get; set; }
        [XmlElement("vehicle_type")] public string VehicleType { get; set; }
        [XmlElement("body_type")] public string BodyType { get; set; }
        [XmlElement("body_subtype")] public string BodySubtype { get; set; }
        [XmlElement("oem_body_style")] public string OemBodyStyle { get; set; }
        [XmlElement("doors")] public int? Doors { get; set; }
        [XmlElement("oem_doors")] public int? OemDoors { get; set; }
        [XmlElement("model_number")] public string ModelNumber { get; set; }
        [XmlElement("package_code")] public string PackageCode { get; set; }
        [XmlElement("package_summary")] public string PackageSummary { get; set; }
        [XmlElement("rear_axle")] public string RearAxle { get; set; }
        [XmlElement("drive_type")] public string DriveType { get; set; }
        [XmlElement("brake_system")] public string BrakeSystem { get; set; }
        [XmlElement("restraint_type")] public string RestraintType { get; set; }
        [XmlElement("country_of_manufacture")] public string CountryOfManufacture { get; set; }
        [XmlElement("plant")] public string Plant { get; set; }
    }

    public class Pricing {
        [XmlElement("msrp")] public decimal? Msrp { get; set; }
        [XmlElement("invoice_price")] public decimal? InvoicePrice { get; set; }
        [XmlElement("destination_charge")] public decimal? DestinationCharge { get; set; }
        [XmlElement("gas_guzzler_tax")] public decimal? GasGuzzlerTax { get; set; }
    }

    public class Engines {
        [XmlElement("engine")]
        public List<Engine> Items { get; set; } = new();
    }

    public class Engine {
        [XmlAttribute("name")] public string Name { get; set; }
        [XmlAttribute("brand")] public string Brand { get; set; }
        [XmlAttribute("marketing_name")] public string MarketingName { get; set; }
        [XmlAttribute("engine_id")] public long EngineId { get; set; }

        [XmlElement("availability")] public string Availability { get; set; }
        [XmlElement("ice_aspiration")] public string Aspiration { get; set; }
        [XmlElement("ice_block_type")] public string BlockType { get; set; }
        [XmlElement("ice_bore")] public decimal? Bore { get; set; }
        [XmlElement("ice_cam_type")] public string CamType { get; set; }
        [XmlElement("ice_compression")] public decimal? Compression { get; set; }
        [XmlElement("ice_cylinders")] public int? Cylinders { get; set; }
        [XmlElement("ice_displacement")] public decimal? DisplacementL { get; set; }
        [XmlElement("electric_motor_configuration")] public string ElectricMotorConfiguration { get; set; }
        [XmlElement("electric_max_hp")] public int? ElectricMaxHp { get; set; }
        [XmlElement("electric_max_kw")] public int? ElectricMaxKw { get; set; }
        [XmlElement("electric_max_torque")] public int? ElectricMaxTorque { get; set; }
        [XmlElement("engine_type")] public string EngineType { get; set; }
        [XmlElement("ice_fuel_induction")] public string FuelInduction { get; set; }
        [XmlElement("fuel_quality")] public int? FuelQualityOctane { get; set; }
        [XmlElement("fuel_type")] public string FuelType { get; set; }
        [XmlElement("fleet")] public string Fleet { get; set; }
        [XmlElement("generator_description")] public string GeneratorDescription { get; set; }
        [XmlElement("generator_max_hp")] public int? GeneratorMaxHp { get; set; }
        [XmlElement("invoice_price")] public decimal? InvoicePrice { get; set; }
        [XmlElement("ice_max_hp")] public int? MaxHp { get; set; }
        [XmlElement("ice_max_hp_at")] public int? MaxHpAtRpm { get; set; }
        [XmlElement("max_payload")] public string MaxPayload { get; set; }
        [XmlElement("ice_max_torque")] public int? MaxTorqueLbFt { get; set; }
        [XmlElement("ice_max_torque_at")] public int? MaxTorqueAtRpm { get; set; }
        [XmlElement("msrp")] public decimal? Msrp { get; set; }
        [XmlElement("oil_capacity")] public decimal? OilCapacityQt { get; set; }
        [XmlElement("order_code")] public string OrderCode { get; set; }
        [XmlElement("redline")] public int? RedlineRpm { get; set; }
        [XmlElement("ice_stroke")] public decimal? Stroke { get; set; }
        [XmlElement("total_max_hp")] public int? TotalMaxHp { get; set; }
        [XmlElement("total_max_hp_at")] public int? TotalMaxHpAtRpm { get; set; }
        [XmlElement("total_max_torque")] public int? TotalMaxTorque { get; set; }
        [XmlElement("total_max_torque_at")] public int? TotalMaxTorqueAtRpm { get; set; }
        [XmlElement("ice_valve_timing")] public string ValveTiming { get; set; }
        [XmlElement("ice_valves")] public int? Valves { get; set; }
    }

    public class Transmissions {
        [XmlElement("transmission")]
        public List<Transmission> Items { get; set; } = new();
    }

    public class Transmission {
        [XmlAttribute("name")] public string Name { get; set; }
        [XmlAttribute("brand")] public string Brand { get; set; }
        [XmlAttribute("marketing_name")] public string MarketingName { get; set; }
        [XmlAttribute("transmission_id")] public long TransmissionId { get; set; }

        [XmlElement("availability")] public string Availability { get; set; }
        [XmlElement("type")] public string Type { get; set; } // e.g., "A"
        [XmlElement("detail_type")] public string DetailType { get; set; }
        [XmlElement("gears")] public int? Gears { get; set; }
        [XmlElement("order_code")] public string OrderCode { get; set; }
        [XmlElement("msrp")] public decimal? Msrp { get; set; }
        [XmlElement("invoice_price")] public decimal? InvoicePrice { get; set; }
        [XmlElement("fleet")] public string Fleet { get; set; }
    }

    #endregion

    #region Generic Equipment (shared for standard/optional)

    public class EquipmentSection {
        [XmlElement("generic_equipment_category_group")]
        public List<GenericEquipmentCategoryGroup> Groups { get; set; } = new();
    }

    public class GenericEquipmentCategoryGroup {
        [XmlAttribute("name")]
        public string Name { get; set; }

        [XmlElement("generic_equipment_category")]
        public List<GenericEquipmentCategory> Categories { get; set; } = new();
    }

    public class GenericEquipmentCategory {
        [XmlAttribute("name")]
        public string Name { get; set; }

        [XmlElement("generic_equipment")]
        public List<GenericEquipment> Equipments { get; set; } = new();
    }

    public class GenericEquipment {
        [XmlAttribute("name")]
        public string Name { get; set; }

        [XmlElement("generic_equipment_value")]
        public List<GenericEquipmentValue> Values { get; set; } = new();
    }

    public class GenericEquipmentValue {
        [XmlAttribute("id")]
        public int Id { get; set; }

        // Optional attribute like installed="UK"
        [XmlAttribute("installed")]
        public string Installed { get; set; }

        // The text content (e.g., "Bluetooth", "5.1", "590")
        [XmlText]
        public string Value { get; set; }
    }

    #endregion

    #region Colors & Supplemental (flexible placeholders)

    public class Colors {
        // Capture whatever structure your provider supplies.
        [XmlAnyElement]
        public XmlElement[] Any { get; set; }
    }

    public class SupplementalData {
        [XmlAnyElement]
        public XmlElement[] Any { get; set; }
    }

    #endregion

    #region Warranties

    public class Warranties {
        [XmlElement("warranty")]
        public List<Warranty> Items { get; set; } = new();
    }

    public class Warranty {
        [XmlAttribute("name")]
        public string Name { get; set; }

        [XmlElement("type")]
        public string Type { get; set; }

        [XmlElement("months")]
        public int? Months { get; set; }

        [XmlElement("miles")]
        public int? Miles { get; set; }
    }

    #endregion

    #region EPA

    public class EpaFuelEfficiency {
        [XmlElement("epa_mpg_record")]
        public List<EpaMpgRecord> Records { get; set; } = new();
    }

    public class EpaMpgRecord {
        [XmlAttribute("engine_id")] public long EngineId { get; set; }
        [XmlAttribute("transmission_id")] public long TransmissionId { get; set; }
        [XmlAttribute("fuel_type")] public string FuelType { get; set; }
        [XmlAttribute("fuel_grade")] public string FuelGrade { get; set; }

        [XmlElement("city")] public int? City { get; set; }
        [XmlElement("highway")] public int? Highway { get; set; }
        [XmlElement("combined")] public int? Combined { get; set; }
    }

    public class EpaGreenScores {
        [XmlElement("epa_green_score_record")]
        public List<EpaGreenScoreRecord> Records { get; set; } = new();
    }

    public class EpaGreenScoreRecord {
        [XmlAttribute("engine_id")] public long EngineId { get; set; }
        [XmlAttribute("transmission_id")] public long TransmissionId { get; set; }
        [XmlAttribute("fuel_type")] public string FuelType { get; set; }
        [XmlAttribute("underhood_id")] public string UnderhoodId { get; set; }
        [XmlAttribute("sales_area")] public string SalesArea { get; set; }

        [XmlElement("emissions_standard")] public string EmissionsStandard { get; set; }
        [XmlElement("air_pollution_score")] public int? AirPollutionScore { get; set; }
        [XmlElement("greenhouse_gas_score")] public int? GreenhouseGasScore { get; set; }
        [XmlElement("smartway")] public string Smartway { get; set; } // "Y"/"N"
    }

    #endregion

    #region NHTSA Crash Test Ratings

    public class NhtsaCrashTestRatings {
        [XmlElement("overall_stars")] public int? OverallStars { get; set; }
        [XmlElement("front_crash_overall_stars")] public int? FrontCrashOverallStars { get; set; }
        [XmlElement("front_crash_driver_stars")] public int? FrontCrashDriverStars { get; set; }
        [XmlElement("front_crash_passenger_stars")] public int? FrontCrashPassengerStars { get; set; }
        [XmlElement("side_crash_overall_stars")] public int? SideCrashOverallStars { get; set; }
        [XmlElement("side_barrier_overall_stars")] public int? SideBarrierOverallStars { get; set; }
        [XmlElement("side_barrier_driver_stars")] public int? SideBarrierDriverStars { get; set; }
        [XmlElement("side_barrier_passenger_stars")] public int? SideBarrierPassengerStars { get; set; }
        [XmlElement("side_pole_driver_stars")] public int? SidePoleDriverStars { get; set; }
        [XmlElement("side_combined_front_stars")] public int? SideCombinedFrontStars { get; set; }
        [XmlElement("side_combined_rear_stars")] public int? SideCombinedRearStars { get; set; }
        [XmlElement("rollover_stars")] public int? RolloverStars { get; set; }

        [XmlElement("front_crash_driver_comment")] public string FrontCrashDriverComment { get; set; }
        [XmlElement("front_crash_passenger_comment")] public string FrontCrashPassengerComment { get; set; }
        [XmlElement("side_barrier_driver_comment")] public string SideBarrierDriverComment { get; set; }
        [XmlElement("side_barrier_passenger_comment")] public string SideBarrierPassengerComment { get; set; }
        [XmlElement("rollover_comment")] public string RolloverComment { get; set; }
    }

    #endregion
}
