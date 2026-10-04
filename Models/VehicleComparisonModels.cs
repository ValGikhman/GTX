using System.Collections.Generic;

namespace GTX.Models
{
    public class VehicleComparisonViewModel
    {
        public VehicleComparisonViewModel()
        {
            Vehicles = new List<VehicleComparisonVehicle>();
            Sections = new List<VehicleComparisonSection>();
        }

        public List<VehicleComparisonVehicle> Vehicles { get; set; }
        public List<VehicleComparisonSection> Sections { get; set; }
        public VehicleComparisonAiAnalysis Analysis { get; set; }
        public string AiNotice { get; set; }
    }

    public class VehicleComparisonVehicle
    {
        public string Stock { get; set; }
        public string Title { get; set; }
        public string Subtitle { get; set; }
        public string DetailsUrl { get; set; }
        public string ImageUrl { get; set; }
        public bool HasDataOne { get; set; }
    }

    public class VehicleComparisonSection
    {
        public VehicleComparisonSection()
        {
            Rows = new List<VehicleComparisonRow>();
        }

        public string Name { get; set; }
        public string Icon { get; set; }
        public List<VehicleComparisonRow> Rows { get; set; }
    }

    public class VehicleComparisonRow
    {
        public VehicleComparisonRow()
        {
            Values = new List<string>();
            Highlights = new List<bool>();
        }

        public string Label { get; set; }
        public List<string> Values { get; set; }
        public List<bool> Highlights { get; set; }
    }

    public class VehicleComparisonAiAnalysis
    {
        public VehicleComparisonAiAnalysis()
        {
            Recommendations = new List<VehicleComparisonAiRecommendation>();
            Caveats = new List<string>();
        }

        public string Summary { get; set; }
        public List<VehicleComparisonAiRecommendation> Recommendations { get; set; }
        public List<string> Caveats { get; set; }
    }

    public class VehicleComparisonAiRecommendation
    {
        public string Stock { get; set; }
        public string BestFor { get; set; }
        public string Reason { get; set; }
    }
}
