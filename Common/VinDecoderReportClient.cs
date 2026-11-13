using Services;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;

namespace GTX.Common {
    internal sealed class VinDecoderReportException : Exception {
        internal int StatusCode { get; }
        internal VinDecoderReportException(int statusCode, string message) : base(message) { StatusCode = statusCode; }
    }

    internal static class VinDecoderReportClient {
        internal static bool IsProvider(string provider) { return provider == "dataone" || provider == "neovin"; }

        internal static async Task<VinDecoderReportDTO> FetchAsync(HttpClient client, Uri origin, string apiKey, string vin, string provider) {
            vin = Controllers.VinDecoderController.NormalizeVin(vin);
            if (!IsProvider(provider) || vin == null)
                throw new VinDecoderReportException(400, "Select a VIN decoder and enter a valid VIN.");
            if (origin == null || !origin.IsAbsoluteUri || origin.Scheme != Uri.UriSchemeHttps
                || !string.IsNullOrEmpty(origin.UserInfo) || !string.IsNullOrEmpty(origin.Query)
                || !string.IsNullOrEmpty(origin.Fragment) || string.IsNullOrWhiteSpace(apiKey))
                throw new VinDecoderReportException(503, "Configure the vehicle research API URL and key in Services.");

            var neo = provider == "neovin";
            var endpoint = new Uri(origin, neo ? "/api/service/neovin" : "/api/service/vin/" + Uri.EscapeDataString(vin) + "/report");
            try {
                using (var request = new HttpRequestMessage(neo ? HttpMethod.Post : HttpMethod.Get, endpoint)) {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey.Trim());
                    request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                    if (neo) request.Content = new StringContent(new JObject { ["Vin"] = vin }.ToString(Newtonsoft.Json.Formatting.None), Encoding.UTF8, "application/json");
                    using (var response = await client.SendAsync(request)) {
                        if ((int)response.StatusCode == 429) throw new VinDecoderReportException(429, "The VIN decoding limit has been reached. Please try again later.");
                        if (response.StatusCode == HttpStatusCode.ServiceUnavailable) throw new VinDecoderReportException(503, "The selected VIN decoding service is unavailable.");
                        if (!response.IsSuccessStatusCode) throw InvalidResponse();
                        if (response.Content == null || !string.Equals(response.Content.Headers.ContentType?.MediaType, "application/json", StringComparison.OrdinalIgnoreCase)) throw InvalidResponse();
                        var body = JObject.Parse(await response.Content.ReadAsStringAsync());
                        if (!string.Equals(Text(body, "vin"), vin, StringComparison.OrdinalIgnoreCase)) throw InvalidResponse();
                        var raw = Text(body, neo ? "rawJson" : "rawXml");
                        var html = Text(body, "html");
                        if (string.IsNullOrWhiteSpace(raw) || string.IsNullOrWhiteSpace(html)) throw InvalidResponse();
                        if (neo) {
                            if (!(JToken.Parse(raw) is JObject)) throw InvalidResponse();
                        } else {
                            var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
                            using (var text = new StringReader(raw))
                            using (var reader = XmlReader.Create(text, settings)) {
                                if (XElement.Load(reader).Name != "decoded_data") throw InvalidResponse();
                            }
                        }
                        var timestamp = body["decodedAtUtc"];
                        if (timestamp == null || (timestamp.Type != JTokenType.String && timestamp.Type != JTokenType.Date)
                            || !DateTimeOffset.TryParse((string)timestamp, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var decoded)) throw InvalidResponse();
                        return new VinDecoderReportDTO {
                            Vin = vin, Provider = provider, PayloadFormat = neo ? "json" : "xml",
                            FormatterKey = neo ? "neovin-v1" : "dataone-v1", RawPayload = raw, Html = html,
                            DecodedUtc = decoded.UtcDateTime
                        };
                    }
                }
            } catch (OperationCanceledException) {
                throw new VinDecoderReportException(504, "VIN decoding timed out. Please try again.");
            } catch (Exception ex) when (ex is HttpRequestException || ex is JsonException || ex is XmlException || ex is IOException || ex is InvalidCastException) {
                throw InvalidResponse();
            }
        }

        private static string Text(JObject body, string key) { return body[key]?.Type == JTokenType.String ? (string)body[key] : null; }
        private static VinDecoderReportException InvalidResponse() { return new VinDecoderReportException(502, "The VIN decoding service returned an invalid or unsuccessful response."); }
    }
}
