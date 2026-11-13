using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace GTX.Common {
    public sealed class OpenAiModelCatalogException : Exception {
        public OpenAiModelCatalogException(string message) : base(message) { }
    }

    public sealed class OpenAiModelCatalog {
        private static readonly HttpClient SharedClient = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) {
            Timeout = Timeout.InfiniteTimeSpan,
            MaxResponseContentBufferSize = 2 * 1024 * 1024
        };
        private readonly HttpClient client;

        public OpenAiModelCatalog() : this(SharedClient) { }
        public OpenAiModelCatalog(HttpClient client) {
            this.client = client ?? throw new ArgumentNullException(nameof(client));
        }

        public async Task<string[]> ListAsync(string apiKey) {
            var key = (apiKey ?? "").Trim();
            if (key.Length == 0) throw new OpenAiModelCatalogException("Enter an OpenAI API key to load its models.");
            if (key.Length > 2048 || key.Any(c => char.IsControl(c) || char.IsWhiteSpace(c)))
                throw new OpenAiModelCatalogException("Enter a valid OpenAI API key to load its models.");

            try {
                // Never place credentials in a URL or follow a redirect to another host.
                using (var request = new HttpRequestMessage(HttpMethod.Get, "https://api.openai.com/v1/models"))
                using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10))) {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
                    using (var response = await client.SendAsync(request, HttpCompletionOption.ResponseContentRead, timeout.Token)) {
                        if (response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Forbidden)
                            throw new OpenAiModelCatalogException("OpenAI rejected this API key or its access to the models list.");
                        if ((int)response.StatusCode == 429)
                            throw new OpenAiModelCatalogException("OpenAI's rate limit was reached. Try refreshing again shortly.");
                        if (!response.IsSuccessStatusCode)
                            throw new OpenAiModelCatalogException("OpenAI could not load the models list. Try again.");

                        var body = response.Content == null ? "" : await response.Content.ReadAsStringAsync();
                        var data = JObject.Parse(body)["data"] as JArray;
                        if (data == null) throw new OpenAiModelCatalogException("OpenAI returned an invalid models list. Try refreshing again.");
                        // The endpoint lists multiple model families, without chatbot capability metadata.
                        return data.OfType<JObject>()
                            .Where(item => item["id"]?.Type == JTokenType.String)
                            .Select(item => (string)item["id"])
                            .Where(id => !string.IsNullOrWhiteSpace(id) && id.Length <= 2048 && !id.Any(char.IsControl))
                            .Distinct(StringComparer.Ordinal)
                            .OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
                            .ThenBy(id => id, StringComparer.Ordinal)
                            .ToArray();
                    }
                }
            }
            catch (OperationCanceledException) {
                throw new OpenAiModelCatalogException("OpenAI took too long to respond. Try refreshing again.");
            }
            catch (HttpRequestException) {
                throw new OpenAiModelCatalogException("OpenAI could not be reached. Try refreshing again.");
            }
            catch (JsonException) {
                throw new OpenAiModelCatalogException("OpenAI returned an invalid models list. Try refreshing again.");
            }
        }
    }
}
