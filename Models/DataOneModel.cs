using System.Collections.Generic;
using System.Xml.Serialization;

namespace GTX.Models {
    // ---------- ROOT ----------
    [XmlRoot("decoded_data")]
    public class DecodedData {
        [XmlElement("decoder_messages")]
        public DecoderMessages DecoderMessages { get; set; }
        [XmlElement("query_responses")]
        public QueryResponses QueryResponses { get; set; }
        [XmlElement("supplemental_data")]
        public string SupplementalData { get; set; } // empty element in sample
    }

    // ---------- HEADER ----------
    public class DecoderMessages {
        [XmlElement("service_provider")]
        public string ServiceProvider { get; set; }
        [XmlElement("decoder_version")]
        public string DecoderVersion { get; set; }
        [XmlElement("decoder_errors")]
        public DecoderErrors DecoderErrors { get; set; } // empty in sample
    }

    public class DecoderErrors
    {
        [XmlElement("error")]
        public DecoderError[] Errors { get; set; }
    }

    public class DecoderError
    {
        [XmlElement("code")]
        public string Code { get; set; }

        [XmlElement("message")]
        public string Message { get; set; }
    }

    // ---------- RESPONSES ----------
    public class QueryResponses {
        [XmlElement("query_response")]
        public List<QueryResponse> Items { get; set; }
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
    }

    public class QueryError {
        [XmlElement("error_code")]
        public string ErrorCode { get; set; }
        [XmlElement("error_message")]
        public string ErrorMessage { get; set; }
    }

    // ---------- US MARKET ----------
    public class UsMarketData {
        [XmlElement("us_styles")]
        public UsStyles UsStyles { get; set; }
    }

    public class UsStyles {
        [XmlAttribute("count")]
        public string Count { get; set; }
        [XmlElement("style")]
        public List<Style> Styles { get; set; }
    }

    public class Style {
        [XmlAttribute("name")]
        public string Name { get; set; }
        [XmlAttribute("vehicle_id")]
        public string VehicleId { get; set; }
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
        [XmlElement("standard_specifications")]
        public StandardSpecifications StandardSpecifications { get; set; }
        [XmlElement("standard_generic_equipment")]
        public GenericEquipmentGroups StandardGenericEquipment { get; set; }
        [XmlElement("optional_generic_equipment")]
        public GenericEquipmentGroups OptionalGenericEquipment { get; set; }
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

    // ---------- BASIC / PRICING ----------
    public class BasicData {
        [XmlElement("market")]
        public string Market { get; set; }
        [XmlElement("year")]
        public string Year { get; set; }
        [XmlElement("make")]
        public string Make { get; set; }
        [XmlElement("model")]
        public string Model { get; set; }
        [XmlElement("trim")]
        public string Trim { get; set; }
        [XmlElement("vehicle_type")]
        public string VehicleType { get; set; }
        [XmlElement("body_type")]
        public string BodyType { get; set; }
        [XmlElement("body_subtype")]
        public string BodySubtype { get; set; }
        [XmlElement("oem_body_style")]
        public string OemBodyStyle { get; set; }
        [XmlElement("doors")]
        public string Doors { get; set; }
        [XmlElement("oem_doors")]
        public string OemDoors { get; set; }
        [XmlElement("model_number")]
        public string ModelNumber { get; set; }
        [XmlElement("package_code")]
        public string PackageCode { get; set; }
        [XmlElement("package_summary")]
        public string PackageSummary { get; set; }
        [XmlElement("rear_axle")]
        public string RearAxle { get; set; }
        [XmlElement("drive_type")]
        public string DriveType { get; set; }
        [XmlElement("brake_system")]
        public string BrakeSystem { get; set; }
        [XmlElement("restraint_type")]
        public string RestraintType { get; set; }
        [XmlElement("country_of_manufacture")]
        public string CountryOfManufacture { get; set; }
        [XmlElement("plant")]
        public string Plant { get; set; }
    }

    public class Pricing {
        [XmlElement("msrp")]
        public string Msrp { get; set; }
        [XmlElement("invoice_price")]
        public string InvoicePrice { get; set; }
        [XmlElement("destination_charge")]
        public string DestinationCharge { get; set; }
        [XmlElement("gas_guzzler_tax")]
        public string GasGuzzlerTax { get; set; }
    }

    // ---------- ENGINES / TRANSMISSIONS ----------
    public class Engines {
        [XmlElement("engine")]
        public List<Engine> Items { get; set; }
    }

    public class Engine {
        [XmlAttribute("name")]
        public string Name { get; set; }
        [XmlAttribute("brand")]
        public string Brand { get; set; }
        [XmlAttribute("marketing_name")]
        public string MarketingName { get; set; }
        [XmlAttribute("engine_id")]
        public string EngineId { get; set; }

        [XmlElement("availability")]
        public string Availability { get; set; }
        [XmlElement("ice_aspiration")]
        public string IceAspiration { get; set; }
        [XmlElement("ice_block_type")]
        public string IceBlockType { get; set; }
        [XmlElement("ice_bore")]
        public string IceBore { get; set; }
        [XmlElement("ice_cam_type")]
        public string IceCamType { get; set; }
        [XmlElement("ice_compression")]
        public string IceCompression { get; set; }
        [XmlElement("ice_cylinders")]
        public string IceCylinders { get; set; }
        [XmlElement("ice_displacement")]
        public string IceDisplacement { get; set; }
        [XmlElement("electric_motor_configuration")]
        public string ElectricMotorConfiguration { get; set; }
        [XmlElement("electric_max_hp")]
        public string ElectricMaxHp { get; set; }
        [XmlElement("electric_max_kw")]
        public string ElectricMaxKw { get; set; }
        [XmlElement("electric_max_torque")]
        public string ElectricMaxTorque { get; set; }
        [XmlElement("engine_type")]
        public string EngineType { get; set; }
        [XmlElement("ice_fuel_induction")]
        public string IceFuelInduction { get; set; }
        [XmlElement("fuel_quality")]
        public string FuelQuality { get; set; }
        [XmlElement("fuel_type")]
        public string FuelType { get; set; }
        [XmlElement("fleet")]
        public string Fleet { get; set; }
        [XmlElement("generator_description")]
        public string GeneratorDescription { get; set; }
        [XmlElement("generator_max_hp")]
        public string GeneratorMaxHp { get; set; }
        [XmlElement("invoice_price")]
        public string InvoicePrice { get; set; }
        [XmlElement("ice_max_hp")]
        public string IceMaxHp { get; set; }
        [XmlElement("ice_max_hp_at")]
        public string IceMaxHpAt { get; set; }
        [XmlElement("max_payload")]
        public string MaxPayload { get; set; }
        [XmlElement("ice_max_torque")]
        public string IceMaxTorque { get; set; }
        [XmlElement("ice_max_torque_at")]
        public string IceMaxTorqueAt { get; set; }
        [XmlElement("msrp")]
        public string Msrp { get; set; }
        [XmlElement("oil_capacity")]
        public string OilCapacity { get; set; }
        [XmlElement("order_code")]
        public string OrderCode { get; set; }
        [XmlElement("redline")]
        public string Redline { get; set; }
        [XmlElement("ice_stroke")]
        public string IceStroke { get; set; }
        [XmlElement("total_max_hp")]
        public string TotalMaxHp { get; set; }
        [XmlElement("total_max_hp_at")]
        public string TotalMaxHpAt { get; set; }
        [XmlElement("total_max_torque")]
        public string TotalMaxTorque { get; set; }
        [XmlElement("total_max_torque_at")]
        public string TotalMaxTorqueAt { get; set; }
        [XmlElement("ice_valve_timing")]
        public string IceValveTiming { get; set; }
        [XmlElement("ice_valves")]
        public string IceValves { get; set; }
    }

    public class Transmissions {
        [XmlElement("transmission")]
        public List<Transmission> Items { get; set; }
    }

    public class Transmission {
        [XmlAttribute("name")]
        public string Name { get; set; }
        [XmlAttribute("brand")]
        public string Brand { get; set; }
        [XmlAttribute("marketing_name")]
        public string MarketingName { get; set; }
        [XmlAttribute("transmission_id")]
        public string TransmissionId { get; set; }

        [XmlElement("availability")]
        public string Availability { get; set; }
        [XmlElement("type")]
        public string Type { get; set; }
        [XmlElement("detail_type")]
        public string DetailType { get; set; }
        [XmlElement("gears")]
        public string Gears { get; set; }
        [XmlElement("order_code")]
        public string OrderCode { get; set; }
        [XmlElement("msrp")]
        public string Msrp { get; set; }
        [XmlElement("invoice_price")]
        public string InvoicePrice { get; set; }
        [XmlElement("fleet")]
        public string Fleet { get; set; }
    }

    // ---------- SPECIFICATIONS ----------
    public class StandardSpecifications {
        [XmlElement("specification_category")]
        public List<SpecificationCategory> Categories { get; set; }
    }

    public class SpecificationCategory {
        [XmlAttribute("name")]
        public string Name { get; set; }
        [XmlElement("specification_value")]
        public List<SpecificationValue> Values { get; set; }
    }

    public class SpecificationValue {
        [XmlAttribute("id")]
        public string Id { get; set; }
        [XmlAttribute("name")]
        public string Name { get; set; }
        [XmlText]
        public string Value { get; set; }
    }

    // ---------- GENERIC EQUIPMENT (standard + optional share same shapes) ----------
    public class GenericEquipmentGroups {
        [XmlElement("generic_equipment_category_group")]
        public List<GenericEquipmentCategoryGroup> Groups { get; set; }
    }

    public class GenericEquipmentCategoryGroup {
        [XmlAttribute("name")]
        public string Name { get; set; }
        [XmlElement("generic_equipment_category")]
        public List<GenericEquipmentCategory> Categories { get; set; }
    }

    public class GenericEquipmentCategory {
        [XmlAttribute("name")]
        public string Name { get; set; }
        [XmlElement("generic_equipment")]
        public List<GenericEquipment> Equipments { get; set; }
    }

    public class GenericEquipment {
        [XmlAttribute("name")]
        public string Name { get; set; }
        [XmlElement("generic_equipment_value")]
        public List<GenericEquipmentValue> Values { get; set; }
    }

    public class GenericEquipmentValue {
        [XmlAttribute("id")]
        public string Id { get; set; }
        [XmlAttribute("installed")]
        public string Installed { get; set; } // "UK" in sample (i.e., optional)
        [XmlText]
        public string Value { get; set; }
    }

    // ---------- COLORS ----------
    public class Colors {
        [XmlElement("exterior_colors")]
        public ExteriorColors Exterior { get; set; }
        [XmlElement("interior_colors")]
        public InteriorColors Interior { get; set; }

        [XmlIgnore]
        [XmlElement("roof_colors")]
        public string RoofColors { get; set; } // empty in sample
    }

    public class ExteriorColors {
        [XmlElement("color")]
        public List<ExteriorColor> Colors { get; set; }
    }

    public class InteriorColors {
        [XmlElement("color")]
        public List<InteriorColor> Colors { get; set; }
    }

    public class ExteriorColor {
        [XmlAttribute("ext_color_id")]
        public string ExtColorId { get; set; }
        [XmlAttribute("mfr_code")]
        public string MfrCode { get; set; }
        [XmlAttribute("two_tone")]
        public string TwoTone { get; set; }

        [XmlElement("generic_color_name")]
        public string GenericColorName { get; set; }
        [XmlElement("mfr_color_name")]
        public string MfrColorName { get; set; }
        [XmlElement("primary_rgb_code")]
        public RgbCode PrimaryRgbCode { get; set; }
        [XmlElement("secondary_rgb_code")]
        public RgbCode SecondaryRgbCode { get; set; }
        [XmlElement("fleet")]
        public string Fleet { get; set; }
    }

    public class InteriorColor {
        [XmlAttribute("int_color_id")]
        public string IntColorId { get; set; }
        [XmlAttribute("mfr_code")]
        public string MfrCode { get; set; }
        [XmlAttribute("two_tone")]
        public string TwoTone { get; set; }

        [XmlElement("generic_color_name")]
        public string GenericColorName { get; set; }
        [XmlElement("mfr_color_name")]
        public string MfrColorName { get; set; }
        [XmlElement("primary_rgb_code")]
        public RgbCode PrimaryRgbCode { get; set; }
        [XmlElement("secondary_rgb_code")]
        public RgbCode SecondaryRgbCode { get; set; }
        [XmlElement("fleet")]
        public string Fleet { get; set; }
    }

    public class RgbCode {
        [XmlAttribute("r")]
        public string R { get; set; }
        [XmlAttribute("g")]
        public string G { get; set; }
        [XmlAttribute("b")]
        public string B { get; set; }
        [XmlAttribute("hex")]
        public string Hex { get; set; }
    }

    // ---------- WARRANTIES ----------
    public class Warranties {
        [XmlElement("warranty")]
        public List<Warranty> Items { get; set; }
    }

    public class Warranty {
        [XmlAttribute("name")]
        public string Name { get; set; }
        [XmlElement("type")]
        public string Type { get; set; }
        [XmlElement("months")]
        public string Months { get; set; }
        [XmlElement("miles")]
        public string Miles { get; set; }
    }

    // ---------- EPA ----------
    public class EpaFuelEfficiency {
        [XmlElement("epa_mpg_record")]
        public List<EpaMpgRecord> Records { get; set; }
    }

    public class EpaMpgRecord {
        [XmlAttribute("engine_id")]
        public string EngineId { get; set; }
        [XmlAttribute("transmission_id")]
        public string TransmissionId { get; set; }
        [XmlAttribute("fuel_type")]
        public string FuelType { get; set; }
        [XmlAttribute("fuel_grade")]
        public string FuelGrade { get; set; }

        [XmlElement("city")]
        public string City { get; set; }
        [XmlElement("highway")]
        public string Highway { get; set; }
        [XmlElement("combined")]
        public string Combined { get; set; }
    }

    public class EpaGreenScores {
        [XmlElement("epa_green_score_record")]
        public List<EpaGreenScoreRecord> Records { get; set; }
    }

    public class EpaGreenScoreRecord {
        [XmlAttribute("engine_id")]
        public string EngineId { get; set; }
        [XmlAttribute("transmission_id")]
        public string TransmissionId { get; set; }
        [XmlAttribute("fuel_type")]
        public string FuelType { get; set; }
        [XmlAttribute("underhood_id")]
        public string UnderhoodId { get; set; }
        [XmlAttribute("sales_area")]
        public string SalesArea { get; set; }

        [XmlElement("emissions_standard")]
        public string EmissionsStandard { get; set; }
        [XmlElement("air_pollution_score")]
        public string AirPollutionScore { get; set; }
        [XmlElement("greenhouse_gas_score")]
        public string GreenhouseGasScore { get; set; }
        [XmlElement("smartway")]
        public string Smartway { get; set; }
    }

    // ---------- NHTSA ----------
    public class NhtsaCrashTestRatings {
        [XmlElement("overall_stars")]
        public string OverallStars { get; set; }
        [XmlElement("front_crash_overall_stars")]
        public string FrontCrashOverallStars { get; set; }
        [XmlElement("front_crash_driver_stars")]
        public string FrontCrashDriverStars { get; set; }
        [XmlElement("front_crash_passenger_stars")]
        public string FrontCrashPassengerStars { get; set; }
        [XmlElement("side_crash_overall_stars")]
        public string SideCrashOverallStars { get; set; }
        [XmlElement("side_barrier_overall_stars")]
        public string SideBarrierOverallStars { get; set; }
        [XmlElement("side_barrier_driver_stars")]
        public string SideBarrierDriverStars { get; set; }
        [XmlElement("side_barrier_passenger_stars")]
        public string SideBarrierPassengerStars { get; set; }
        [XmlElement("side_pole_driver_stars")]
        public string SidePoleDriverStars { get; set; }
        [XmlElement("side_combined_front_stars")]
        public string SideCombinedFrontStars { get; set; }
        [XmlElement("side_combined_rear_stars")]
        public string SideCombinedRearStars { get; set; }
        [XmlElement("rollover_stars")]
        public string RolloverStars { get; set; }

        [XmlElement("front_crash_driver_comment")]
        public string FrontCrashDriverComment { get; set; }
        [XmlElement("front_crash_passenger_comment")]
        public string FrontCrashPassengerComment { get; set; }
        [XmlElement("side_barrier_driver_comment")]
        public string SideBarrierDriverComment { get; set; }
        [XmlElement("side_barrier_passenger_comment")]
        public string SideBarrierPassengerComment { get; set; }
        [XmlElement("rollover_comment")]
        public string RolloverComment { get; set; }
    }
}
