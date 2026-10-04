using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http.Headers;

namespace GTX.Common
{
    public sealed class OpenAiRateLimitSnapshot
    {
        public string Model { get; set; }
        public long? TokenLimit { get; set; }
        public long? RemainingTokens { get; set; }
        public string TokenReset { get; set; }
        public long? RequestLimit { get; set; }
        public long? RemainingRequests { get; set; }
        public string RequestReset { get; set; }
        public DateTimeOffset CapturedAt { get; set; }
    }

    public static class OpenAiRateLimitHealth
    {
        private static readonly object SyncRoot = new object();
        private static OpenAiRateLimitSnapshot _latest;

        public static void Capture(HttpResponseHeaders headers, string model)
        {
            if (headers == null) return;

            var tokenLimit = ReadLong(headers, "x-ratelimit-limit-tokens");
            var remainingTokens = ReadLong(headers, "x-ratelimit-remaining-tokens");
            var requestLimit = ReadLong(headers, "x-ratelimit-limit-requests");
            var remainingRequests = ReadLong(headers, "x-ratelimit-remaining-requests");

            if (!tokenLimit.HasValue
                && !remainingTokens.HasValue
                && !requestLimit.HasValue
                && !remainingRequests.HasValue)
            {
                return;
            }

            var snapshot = new OpenAiRateLimitSnapshot
            {
                Model = model,
                TokenLimit = tokenLimit,
                RemainingTokens = remainingTokens,
                TokenReset = ReadText(headers, "x-ratelimit-reset-tokens"),
                RequestLimit = requestLimit,
                RemainingRequests = remainingRequests,
                RequestReset = ReadText(headers, "x-ratelimit-reset-requests"),
                CapturedAt = DateTimeOffset.UtcNow
            };

            lock (SyncRoot)
            {
                _latest = snapshot;
            }
        }

        public static OpenAiRateLimitSnapshot GetLatest()
        {
            lock (SyncRoot)
            {
                if (_latest == null) return null;

                return new OpenAiRateLimitSnapshot
                {
                    Model = _latest.Model,
                    TokenLimit = _latest.TokenLimit,
                    RemainingTokens = _latest.RemainingTokens,
                    TokenReset = _latest.TokenReset,
                    RequestLimit = _latest.RequestLimit,
                    RemainingRequests = _latest.RemainingRequests,
                    RequestReset = _latest.RequestReset,
                    CapturedAt = _latest.CapturedAt
                };
            }
        }

        private static long? ReadLong(HttpResponseHeaders headers, string name)
        {
            long value;
            var text = ReadText(headers, name);
            return long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value)
                ? value
                : (long?)null;
        }

        private static string ReadText(HttpResponseHeaders headers, string name)
        {
            IEnumerable<string> values;
            return headers.TryGetValues(name, out values)
                ? values.FirstOrDefault()
                : null;
        }
    }
}
