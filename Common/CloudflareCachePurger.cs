using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace GTX.Helpers
{
    internal static class CloudflareCachePurger
    {
        private const int MaxUrlsPerRequest = 30;

        public static Task<bool> PurgeInventoryImageAsync(string stock, string file)
        {
            return PurgeInventoryImagesAsync(stock, new[] { file });
        }

        public static async Task<bool> PurgeInventoryImagesAsync(string stock, IEnumerable<string> files)
        {
            var zoneId = ConfigurationManager.AppSettings["Cloudflare:Cache:ZoneId"];
            var apiToken = ConfigurationManager.AppSettings["Cloudflare:Cache:ApiToken"];
            if (string.IsNullOrWhiteSpace(zoneId) || string.IsNullOrWhiteSpace(apiToken))
            {
                return false;
            }

            var urls = BuildInventoryImageUrls(stock, files);
            if (urls.Count == 0)
            {
                return false;
            }

            try
            {
                using (var client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromSeconds(20);
                    client.DefaultRequestHeaders.Authorization =
                        new AuthenticationHeaderValue("Bearer", apiToken.Trim());

                    for (var index = 0; index < urls.Count; index += MaxUrlsPerRequest)
                    {
                        var batch = urls.Skip(index).Take(MaxUrlsPerRequest).ToArray();
                        var json = JsonConvert.SerializeObject(new { files = batch });
                        using (var content = new StringContent(json, Encoding.UTF8, "application/json"))
                        using (var response = await client.PostAsync(
                            "https://api.cloudflare.com/client/v4/zones/" + Uri.EscapeDataString(zoneId.Trim()) + "/purge_cache",
                            content))
                        {
                            if (!response.IsSuccessStatusCode)
                            {
                                Trace.TraceWarning(
                                    "Cloudflare cache purge returned HTTP {0} for {1} inventory image(s).",
                                    (int)response.StatusCode,
                                    batch.Length);
                                return false;
                            }
                        }
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                // Image mutations must remain successful even if cache invalidation is temporarily unavailable.
                Trace.TraceWarning("Cloudflare inventory image cache purge failed: {0}", ex.Message);
                return false;
            }
        }

        public static async Task<bool> PurgeInventoryHostAsync()
        {
            var zoneId = ConfigurationManager.AppSettings["Cloudflare:Cache:ZoneId"];
            var apiToken = ConfigurationManager.AppSettings["Cloudflare:Cache:ApiToken"];
            Uri baseUri;
            if (string.IsNullOrWhiteSpace(zoneId) ||
                string.IsNullOrWhiteSpace(apiToken) ||
                !Uri.TryCreate(InventoryImageSettings.BaseUrl, UriKind.Absolute, out baseUri))
            {
                return false;
            }

            try
            {
                using (var client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromSeconds(20);
                    client.DefaultRequestHeaders.Authorization =
                        new AuthenticationHeaderValue("Bearer", apiToken.Trim());

                    var json = JsonConvert.SerializeObject(new { hosts = new[] { baseUri.Host } });
                    using (var content = new StringContent(json, Encoding.UTF8, "application/json"))
                    using (var response = await client.PostAsync(
                        "https://api.cloudflare.com/client/v4/zones/" + Uri.EscapeDataString(zoneId.Trim()) + "/purge_cache",
                        content))
                    {
                        if (!response.IsSuccessStatusCode)
                        {
                            Trace.TraceWarning(
                                "Cloudflare cache purge returned HTTP {0} for inventory image host {1}.",
                                (int)response.StatusCode,
                                baseUri.Host);
                            return false;
                        }
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                Trace.TraceWarning("Cloudflare inventory host cache purge failed: {0}", ex.Message);
                return false;
            }
        }

        private static List<string> BuildInventoryImageUrls(string stock, IEnumerable<string> files)
        {
            var normalizedStock = (stock ?? string.Empty).Trim().Trim('/', '\\');
            if (string.IsNullOrWhiteSpace(normalizedStock))
            {
                return new List<string>();
            }

            return (files ?? Enumerable.Empty<string>())
                .Select(GetFileName)
                .Where(file => !string.IsNullOrWhiteSpace(file))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(file => InventoryImageSettings.BaseUrl + "/"
                    + Uri.EscapeDataString(normalizedStock) + "/"
                    + Uri.EscapeDataString(file))
                .ToList();
        }

        private static string GetFileName(string file)
        {
            var value = (file ?? string.Empty).Replace('\\', '/');
            var queryIndex = value.IndexOf('?');
            if (queryIndex >= 0)
            {
                value = value.Substring(0, queryIndex);
            }

            return Path.GetFileName(value);
        }
    }
}
