using Newtonsoft.Json.Linq;
using System;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace GTX.Common {
    public sealed class AppraisalVehicleIdentity {
        public string Vin { get; set; }
        public string Year { get; set; }
        public string Make { get; set; }
        public string Model { get; set; }
        public string Style { get; set; }
        public string Trim { get; set; }
        public string BodyStyle { get; set; }
        public string DriveType { get; set; }

        public static AppraisalVehicleIdentity FromAutoDealerJson(string requestedVin, JObject payload) {
            var vin = (requestedVin ?? "").Trim().ToUpperInvariant();
            if (!Regex.IsMatch(vin, "^[A-HJ-NPR-Z0-9]{17}$") || payload == null
                || !string.Equals(Text(payload, "vin"), vin, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("The VIN decoder did not return the requested vehicle.");

            var styles = payload.SelectTokens("data..basic_data").OfType<JObject>().ToArray();
            if (styles.Length == 0) throw UnknownIdentity();
            var identities = styles.Select(style => new {
                Year = ValidYear(Text(style, "year")), Make = Text(style, "make"), Model = Text(style, "model")
            }).ToArray();
            var first = identities[0];
            if (identities.Any(item => item.Year == null || item.Make == null || item.Model == null
                || !string.Equals(item.Year, first.Year, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(item.Make, first.Make, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(item.Model, first.Model, StringComparison.OrdinalIgnoreCase)))
                throw UnknownIdentity();

            var bodyStyle = Shared(styles.Select(style => Text(style, "body_type")));
            return new AppraisalVehicleIdentity {
                Vin = vin, Year = identities[0].Year, Make = identities[0].Make, Model = identities[0].Model,
                Trim = Shared(styles.Select(style => Text(style, "trim"))),
                BodyStyle = bodyStyle,
                DriveType = Shared(styles.Select(style => Text(style, "drive_type"))),
                Style = Shared(styles.Select(style => Text(style, "oem_body_style"))) ?? bodyStyle
                    ?? Shared(styles.Select(style => Text(style.Parent?.Parent as JObject, "@name")))
            };
        }

        private static string Text(JObject source, string key) {
            var value = source?[key];
            if (value == null || (value.Type != JTokenType.String && value.Type != JTokenType.Integer)) return null;
            var text = value.ToString().Trim();
            return text.Length == 0 ? null : text;
        }

        private static string ValidYear(string value) {
            int year;
            return value != null && Regex.IsMatch(value, "^[0-9]{4}$")
                && int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out year) && year >= 1886 && year <= 2100
                ? year.ToString(CultureInfo.InvariantCulture) : null;
        }

        private static string Shared(System.Collections.Generic.IEnumerable<string> values) {
            var candidates = values.ToArray();
            return candidates.Length > 0 && candidates.All(value => value != null)
                && candidates.Distinct(StringComparer.OrdinalIgnoreCase).Count() == 1 ? candidates[0] : null;
        }

        private static InvalidOperationException UnknownIdentity() =>
            new InvalidOperationException("The VIN decoder did not return an unambiguous year, make and model.");
    }
}
