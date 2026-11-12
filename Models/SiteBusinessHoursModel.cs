using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;

namespace GTX.Models {
    public sealed class SiteBusinessHoursModel : IValidatableObject {
        [Range(0, 6)]
        public int DayOfWeek { get; set; }
        public bool IsClosed { get; set; }
        [RegularExpression("(?:[01][0-9]|2[0-3]):[0-5][0-9]", ErrorMessage = "Use a valid opening time.")]
        public string OpenTime { get; set; }
        [RegularExpression("(?:[01][0-9]|2[0-3]):[0-5][0-9]", ErrorMessage = "Use a valid closing time.")]
        public string CloseTime { get; set; }
        public string Day => ((System.DayOfWeek)DayOfWeek).ToString();
        public string Description {
            get {
                TimeSpan opens, closes;
                if (IsClosed || !TryTimes(out opens, out closes)) return "Closed";
                return DateTime.Today.Add(opens).ToString("h:mm tt", CultureInfo.GetCultureInfo("en-US"))
                    + " – " + DateTime.Today.Add(closes).ToString("h:mm tt", CultureInfo.GetCultureInfo("en-US"));
            }
        }
        public bool TryTimes(out TimeSpan opens, out TimeSpan closes) {
            var validOpen = TimeSpan.TryParseExact(OpenTime, @"hh\:mm", CultureInfo.InvariantCulture, out opens);
            var validClose = TimeSpan.TryParseExact(CloseTime, @"hh\:mm", CultureInfo.InvariantCulture, out closes);
            return validOpen && validClose && closes > opens;
        }
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext) {
            TimeSpan opens, closes;
            if (!IsClosed && !TryTimes(out opens, out closes))
                yield return new ValidationResult("Enter opening and closing times, with closing later on the same day.", new[] { "OpenTime", "CloseTime" });
        }
    }
}
