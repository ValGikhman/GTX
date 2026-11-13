using Newtonsoft.Json.Linq;
using System;
using System.Globalization;
using System.Linq;

namespace GTX.Common
{
    internal static class AppraisalComparableScope
    {
        private const int OriginalMileageWindow = 20000;

        private static string ListingIdentity(JObject row) =>
            string.IsNullOrWhiteSpace((string)row["vin"]) ? (string)row["listing_id"] : (string)row["vin"];

        // Expand radius first, then the mileage window, until five returned VINs qualify.
        // Provider results stay distinct from the buyer's original search criteria.
        internal static JObject Select(JArray candidates, JObject scope, string source, int maximumRadius = 5000)
        {
            var radius = (int)scope["radius"];
            var targetMiles = (int)scope["miles"];
            var subjectVin = (string)scope["vin"];
            var initialRadius = Math.Min(radius, maximumRadius);
            var effectiveRadius = initialRadius;
            var mileageWindow = OriginalMileageWindow;
            var rows = (candidates ?? new JArray()).OfType<JObject>()
                .Where(row => !string.IsNullOrWhiteSpace(ListingIdentity(row))
                    && !string.Equals((string)row["vin"], subjectVin, StringComparison.OrdinalIgnoreCase)
                    && Number(row["price"]) > 0 && Number(row["price"]) <= 2000000
                    && Number(row["dist"]) >= 0 && Number(row["dist"]) <= maximumRadius
                    && Number(row["miles"]) >= 0 && Number(row["miles"]) <= 2000000)
                .OrderBy(row => Number(row["price"]))
                .GroupBy(ListingIdentity, StringComparer.OrdinalIgnoreCase)
                .Select(group => (JObject)group.First().DeepClone()).ToArray();
            var strict = rows.Where(row => Number(row["dist"]) <= radius
                && Math.Abs(Number(row["miles"]).Value - targetMiles) <= OriginalMileageWindow).ToArray();
            var eligible = rows.Where(row => Number(row["dist"]) <= effectiveRadius
                && Math.Abs(Number(row["miles"]).Value - targetMiles) <= mileageWindow).ToArray();

            while (eligible.Length < 5 && eligible.Length < rows.Length)
            {
                effectiveRadius = Math.Min(maximumRadius, effectiveRadius + 50);
                eligible = rows.Where(row => Number(row["dist"]) <= effectiveRadius
                    && Math.Abs(Number(row["miles"]).Value - targetMiles) <= mileageWindow).ToArray();
                if (eligible.Length >= 5) break;
                if (rows.Any(row => Math.Abs(Number(row["miles"]).Value - targetMiles) > mileageWindow))
                    mileageWindow = Math.Min(2000000, mileageWindow + 25000);
                eligible = rows.Where(row => Number(row["dist"]) <= effectiveRadius
                    && Math.Abs(Number(row["miles"]).Value - targetMiles) <= mileageWindow).ToArray();
            }

            var selected = strict.Concat(eligible.Where(row => !strict.Contains(row))
                .Take(Math.Max(0, 5 - strict.Length))).OrderBy(row => Number(row["price"])).ToArray();
            foreach (var row in selected)
            {
                row["outsideRadius"] = Number(row["dist"]) > radius;
                row["outsideMileage"] = Math.Abs(Number(row["miles"]).Value - targetMiles) > OriginalMileageWindow;
                row["referenceOnly"] = (bool)row["outsideRadius"] || (bool)row["outsideMileage"];
            }

            var note = string.Format(CultureInfo.GetCultureInfo("en-US"),
                "{0}: {1} match your original {2:N0}-mile radius and {3:N0}–{4:N0} odometer range. Showing {5} returned vehicles, ordered by asking price.",
                source, strict.Length, radius, Math.Max(0, targetMiles - OriginalMileageWindow), Math.Min(2000000, targetMiles + OriginalMileageWindow), selected.Length);
            if (selected.Any(row => (bool)row["referenceOnly"]))
                note += string.Format(CultureInfo.GetCultureInfo("en-US"),
                    " Expanded in 50-mile radius and 25,000-mile odometer steps to {0:N0} miles and {1:N0}–{2:N0} odometer miles. Expanded rows are buyer references.",
                    effectiveRadius, Math.Max(0, targetMiles - mileageWindow), Math.Min(2000000, targetMiles + mileageWindow));
            if (selected.Length < 5) note += " The provider returned fewer than five usable matching VINs.";
            if (maximumRadius < radius || maximumRadius == 100)
                note += " This provider search is limited to a 100-mile radius.";
            return new JObject
            {
                ["listings"] = new JArray(selected), ["strictCount"] = strict.Length,
                ["effectiveRadius"] = effectiveRadius, ["mileageWindow"] = mileageWindow, ["note"] = note
            };
        }

        private static decimal? Number(JToken token)
        {
            decimal value;
            return decimal.TryParse((string)token, NumberStyles.Number, CultureInfo.InvariantCulture, out value)
                ? value : (decimal?)null;
        }
    }
}
