using System.ComponentModel.DataAnnotations;

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
    }

    public sealed class ChatBotResponse
    {
        public bool Success { get; set; }
        public string Reply { get; set; }
        public string ResponseId { get; set; }
        public int? TotalVehicleMatches { get; set; }
        public ChatVehicleResult[] Vehicles { get; set; }
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
