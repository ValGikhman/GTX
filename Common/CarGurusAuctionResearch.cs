using GTX.Models;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text.RegularExpressions;

namespace GTX.Common
{
    internal static class CarGurusAuctionResearch
    {
        internal const string SearchTool = "vehicle-listing-search";
        internal const string DetailTool = "vehicle-listing-detail";
        internal static bool Enabled => ServiceConfiguration.Enabled("CarGurus:Enabled");

        internal static bool IsNoListings(string output) => Regex.IsMatch(output ?? "",
            @"\A\s*(?:Found\s+0\s+vehicles\b[^\r\n]*|No\s+(?:vehicles|listings)\s+found(?:\s+matching\s+your\s+search)?[.!]?)\s*\z",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        internal static Match[] ParseListings(string output) {
            var text = (output ?? "").Replace("\r\n", "\n").Replace('\r', '\n');
            var options = RegexOptions.Multiline | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;
            var legacy = Regex.Matches(text,
                @"^[ \t]*-[ \t]+Listing ID[ \t]+(?<id>[0-9]{1,18}):[ \t]*(?<vehicle>[^\n]+?)[ \t]+—[ \t]+(?<ask>[^\n]+?)[ \t]+·[ \t]+(?<location>[^\n]+)", options);
            var cards = Regex.Matches(text,
                @"^[ \t]*###[ \t]+[0-9]+\.[ \t]+(?<vehicle>[^\n]+)\n[ \t]*Listing ID:[ \t]*(?<id>[0-9]{1,18})[ \t]*\n"
                + @"[ \t]*(?<ask>[^\n·]+?)(?:[ \t]*·[^\n]*)?[ \t]*\n[ \t]*Deal:[^\n·]*·[ \t]*(?:(?<miles>[0-9][0-9,]*)[ \t]+miles|[^\n·]+)[ \t]*·[ \t]*(?<location>[^\n]+)", options);
            return legacy.Cast<Match>().Concat(cards.Cast<Match>()).OrderBy(match => match.Index)
                .GroupBy(match => match.Groups["id"].Value).Select(group => group.First()).Take(5).ToArray();
        }

        internal static Dictionary<string, ChatMarketListing> ParseDetails(string output) {
            var details = new Dictionary<string, ChatMarketListing>();
            var text = (output ?? "").Replace("\r\n", "\n").Replace('\r', '\n');
            var options = RegexOptions.Multiline | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;
            var cards = Regex.Matches(text, @"^[ \t]*#[ \t]+[^\n]+\n[\s\S]*?(?=^[ \t]*#[ \t]+|\z)", options)
                .Cast<Match>().Select(match => match.Value);
            var legacy = Regex.Split(text.Trim(), @"\n[ \t]*\n")
                .Where(block => Regex.IsMatch(block, @"^[ \t]*Listing URL:", options));
            foreach (var block in cards.Concat(legacy)) {
                var id = Regex.Match(block,
                    @"^[ \t]*(?:Listing URL:[ \t]*|\[View on CarGurus\]\()https://www\.cargurus\.com/details/(?<id>[0-9]{1,18})(?=[?/#\s)]|$)", options);
                var markdown = Regex.IsMatch(block, @"\A[ \t]*#[ \t]+", options);
                var title = Regex.Match(block, markdown ? @"\A[ \t]*#[ \t]+(?<title>[^\n]+)" : @"\A(?<title>[^\n]+)\nVIN:", options);
                if (!id.Success || !title.Success || details.ContainsKey(id.Groups["id"].Value)) continue;
                var vin = Regex.Match(block, @"^[ \t]*VIN:[ \t]*(?<vin>[A-HJ-NPR-Z0-9]{17})\b", options);
                var price = Regex.Match(block, @"^[ \t]*(?:Price:[ \t]*)?(?<ask>\$[0-9][0-9,]*(?:\.[0-9]{2})?)(?=[ \t·\n]|$)", options);
                var mileage = Regex.Match(block, @"(?:^[ \t]*|\|[ \t]*)Mileage:[ \t]*(?<miles>[0-9][0-9,]*)\b", options);
                var dealer = Regex.Match(block, markdown
                    ? @"^[ \t]*Dealer rating:[^\n·]*·[ \t]*(?<location>[^\n(]+?)(?:[ \t]*\((?:(?<distance>[0-9][0-9,]*)[ \t]+mi[ \t]+away|[^)\n]*)\))?[ \t]*$"
                    : @"^[ \t]*Dealer:[ \t]*(?<location>[^\n·]+)(?:[^\n]*?·[ \t]*(?<distance>[\d,]+)[ \t]+mi\b)?", options);
                details.Add(id.Groups["id"].Value, new ChatMarketListing {
                    Vehicle = title.Groups["title"].Value.Trim(),
                    Vin = vin.Success ? vin.Groups["vin"].Value : null,
                    Ask = price.Success ? price.Groups["ask"].Value : null,
                    Odometer = mileage.Success ? mileage.Groups["miles"].Value : null,
                    Location = dealer.Success ? dealer.Groups["location"].Value.Trim() : null,
                    Distance = dealer.Groups["distance"].Success ? dealer.Groups["distance"].Value : null
                });
            }
            return details;
        }

        internal static JObject Tool(string tool) {
            var configured = ServiceConfiguration.Get("CarGurus:McpServerUrl");
            if (!Uri.TryCreate(configured, UriKind.Absolute, out var endpoint)
                || endpoint.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(endpoint.UserInfo)
                || !string.IsNullOrEmpty(endpoint.Fragment))
                throw new ConfigurationErrorsException("CarGurus:McpServerUrl must be a valid HTTPS service URL.");
            return new JObject {
                ["type"] = "mcp",
                ["server_label"] = "cargurus",
                ["server_url"] = endpoint.AbsoluteUri,
                ["allowed_tools"] = new JArray(tool),
                ["require_approval"] = "never"
            };
        }

    }
}
