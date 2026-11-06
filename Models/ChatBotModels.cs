using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace GTX.Models
{
    public sealed class ChatBotRequest
    {
        [Required]
        [StringLength(100)]
        public string ChatRequestToken { get; set; }

        [Required]
        [StringLength(800)]
        public string Message { get; set; }

        [StringLength(200)]
        public string PreviousResponseId { get; set; }

        [StringLength(32)]
        public string ProgressId { get; set; }
    }

    public sealed class ChatGreetingRequest
    {
        [Required]
        [StringLength(100)]
        public string ChatRequestToken { get; set; }
    }

    public sealed class ChatBotResponse
    {
        public bool Success { get; set; }
        public string Reply { get; set; }
        public string ResponseId { get; set; }
        public bool PrivateReply { get; set; }
        public ChatMarketReply MarketReply { get; set; }
        public int? TotalVehicleMatches { get; set; }
        public string InventoryUrl { get; set; }
        public ChatVehicleResult[] Vehicles { get; set; }
        public ChatNavigationResult Navigation { get; set; }
    }

    public sealed class ChatMarketReply
    {
        public string Kicker { get; set; }
        public string Title { get; set; }
        public string Context { get; set; }
        public ChatMarketMetric[] Metrics { get; set; } = Array.Empty<ChatMarketMetric>();
        public ChatMarketSection[] Sections { get; set; } = Array.Empty<ChatMarketSection>();
        public string Note { get; set; }
        public string ViewUrl { get; set; }
        internal object AppraisalParameters { get; set; }

        internal string ToPlainText()
        {
            var lines = new[] { Kicker, Title, Context }
                .Concat(Metrics.Select(metric => metric.Label + ": " + metric.Value))
                .Concat(Sections.SelectMany(section => new[] { section.Title, section.Context }
                    .Concat(section.Items.Select(item => "• " + item)).Concat(new[] { section.Note })))
                .Concat(new[] { Note });
            return string.Join("\n", lines.Where(line => !string.IsNullOrWhiteSpace(line)));
        }
    }

    public sealed class ChatMarketMetric
    {
        public string Label { get; set; }
        public string Value { get; set; }
        public bool IsPrimary { get; set; }
    }

    public sealed class ChatMarketSection
    {
        public string Title { get; set; }
        public string Context { get; set; }
        public string[] Items { get; set; } = Array.Empty<string>();
        public string Note { get; set; }
    }

    public sealed class ChatNavigationResult
    {
        public string Url { get; set; }
        public string Label { get; set; }
        public bool RequiresLogin { get; set; }
        public string RequiredRole { get; set; }
    }

    public sealed class ChatVehicleResult
    {
        public string Stock { get; set; }
        public string Title { get; set; }
        public int Mileage { get; set; }
        public int Cylinders { get; set; }
        public int AdvertisedPrice { get; set; }
        public int DocumentaryFee { get; set; }
        public int PriceWithDocumentaryFee { get; set; }
        public string Url { get; set; }
    }

    public sealed class ChatLeadRequest
    {
        [Required]
        [StringLength(100)]
        public string ChatRequestToken { get; set; }

        [Required]
        [StringLength(80)]
        public string FirstName { get; set; }

        [Required]
        [StringLength(80)]
        public string LastName { get; set; }

        [Required]
        [RegularExpression(Extensions.RegexPattern.PHONE_NUMBER)]
        public string Phone { get; set; }

        [Required]
        [EmailAddress]
        [StringLength(160)]
        public string Email { get; set; }

        [StringLength(20)]
        public string VehicleStock { get; set; }

        [Range(0, int.MaxValue)]
        public int EmployerId { get; set; }

        [StringLength(1000)]
        public string Message { get; set; }

        [Range(typeof(bool), "true", "true", ErrorMessage = "Consent is required.")]
        public bool Consent { get; set; }
    }
}
