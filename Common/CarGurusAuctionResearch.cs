using Newtonsoft.Json.Linq;
using System.Configuration;

namespace GTX.Common
{
    internal static class CarGurusAuctionResearch
    {
        internal const string SearchTool = "vehicle-listing-search";
        internal static bool Enabled =>
            bool.TryParse(ConfigurationManager.AppSettings["CarGurus:Enabled"], out var enabled) && enabled;

        internal static JObject Tool(string tool) => new JObject
        {
            ["type"] = "mcp",
            ["server_label"] = "cargurus",
            ["server_url"] = "https://mcp-apps.api.cargurus.com/chat-mcp/v1/stream",
            ["allowed_tools"] = new JArray(tool),
            ["require_approval"] = "never"
        };

    }
}
