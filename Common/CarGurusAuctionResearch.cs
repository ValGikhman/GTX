using Newtonsoft.Json.Linq;
using System;
using System.Configuration;

namespace GTX.Common
{
    internal static class CarGurusAuctionResearch
    {
        internal const string SearchTool = "vehicle-listing-search";
        internal const string DetailTool = "vehicle-listing-detail";
        internal static bool Enabled => ServiceConfiguration.Enabled("CarGurus:Enabled");

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
