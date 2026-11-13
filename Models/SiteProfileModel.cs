using System.Collections.Generic;
using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text.RegularExpressions;

namespace GTX.Models {
    public sealed class SiteProfileModel : IValidatableObject {
        [Required, StringLength(120), Display(Name = "Company name")]
        public string CompanyName { get; set; }
        [Required, EmailAddress, StringLength(254)]
        public string Email { get; set; }
        [Required, Phone, StringLength(30)]
        [RegularExpression(@"(?:\+?1[ .-]?)?(?:\([0-9]{3}\)|[0-9]{3})[ .-]?[0-9]{3}[ .-]?[0-9]{4}", ErrorMessage = "Enter a 10-digit US phone number, optionally prefixed with +1. Extensions are not supported.")]
        public string Phone { get; set; }
        [Required, StringLength(160), Display(Name = "Street address")]
        public string StreetAddress { get; set; }
        [StringLength(120), Display(Name = "Address line 2")]
        public string AddressLine2 { get; set; }
        [Required, StringLength(80)]
        public string City { get; set; }
        [Required, StringLength(2), RegularExpression("[A-Za-z]{2}", ErrorMessage = "Enter a two-letter state code.")]
        public string State { get; set; }
        [Required, RegularExpression("[0-9]{5}", ErrorMessage = "Enter a five-digit ZIP code."), Display(Name = "ZIP code")]
        public string ZipCode { get; set; }
        [Range(1, 500), Display(Name = "Research radius (miles)")]
        public int ResearchRadiusMiles { get; set; }
        [Range(0, 10000), Display(Name = "Documentary fee (USD)")]
        public int DocumentaryFee { get; set; }
        [Required]
        public List<SiteBusinessHoursModel> BusinessHours { get; set; } = new List<SiteBusinessHoursModel>();
        [Range(typeof(decimal), "-90", "90")]
        public decimal? Latitude { get; set; }
        [Range(typeof(decimal), "-180", "180")]
        public decimal? Longitude { get; set; }

        public string GetShortCompanyName() {
            return (CompanyName ?? string.Empty).Split((char[])null, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty;
        }

        public string FullAddress => string.Join(", ", new[] {
            StreetAddress, AddressLine2, City, ((State ?? "") + " " + ZipCode).Trim()
        }.Where(part => !string.IsNullOrWhiteSpace(part)));
        public string PhoneDigits {
            get {
                var digits = Regex.Replace(Phone ?? "", "[^0-9]", "");
                return digits.Length == 10 ? "1" + digits : digits;
            }
        }
        public string PhoneUri => "tel:+" + PhoneDigits;

        public OpenHours[] GetOpenHours() => (BusinessHours ?? new List<SiteBusinessHoursModel>()).Select(item => {
            TimeSpan opens, closes;
            var validTimes = item.TryTimes(out opens, out closes);
            var isOpenDay = !item.IsClosed && validTimes;
            return new OpenHours {
                Day = item.Day, Description = item.Description,
                From = isOpenDay ? opens.Hours : 0, To = isOpenDay ? closes.Hours : 0,
                OpenTime = isOpenDay ? (TimeSpan?)opens : null, CloseTime = isOpenDay ? (TimeSpan?)closes : null
            };
        }).ToArray();

        public bool IsOpenAt(DateTime localNow) {
            var hours = GetOpenHours().FirstOrDefault(item => item.Day == localNow.DayOfWeek.ToString());
            return hours != null && hours.IsOpenAt(localNow);
        }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext) {
            if (Latitude.HasValue != Longitude.HasValue)
                yield return new ValidationResult("Enter both map coordinates, or leave both blank.", new[] { "Latitude", "Longitude" });
            if (BusinessHours == null || BusinessHours.Count != 7 || BusinessHours.Any(item => item == null)
                || BusinessHours.Select(item => item.DayOfWeek).Distinct().Count() != 7) {
                yield return new ValidationResult("Provide one business-hours entry for every day of the week.", new[] { "BusinessHours" });
                yield break;
            }
            for (var index = 0; index < BusinessHours.Count; index++) {
                var item = BusinessHours[index];
                var errors = new List<ValidationResult>();
                Validator.TryValidateObject(item, new ValidationContext(item), errors, validateAllProperties: true);
                foreach (var error in errors)
                    yield return new ValidationResult(error.ErrorMessage, error.MemberNames.Select(member => "BusinessHours[" + index + "]." + member));
            }
        }
    }
}
