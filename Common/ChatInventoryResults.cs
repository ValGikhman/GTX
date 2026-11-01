using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Caching;

namespace GTX.Common
{
    public static class ChatInventoryResults
    {
        private const string CachePrefix = "GTX:ChatInventoryResults:";

        public static string Store(IEnumerable<string> stocks)
        {
            var snapshot = (stocks ?? Enumerable.Empty<string>())
                .Where(stock => !string.IsNullOrWhiteSpace(stock))
                .Select(stock => stock.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (snapshot.Length == 0) return null;

            var token = Guid.NewGuid().ToString("N");
            MemoryCache.Default.Set(CachePrefix + token, snapshot,
                DateTimeOffset.UtcNow.AddDays(7));
            return token;
        }

        public static string[] Find(string token)
        {
            Guid parsed;
            if (!Guid.TryParseExact(token, "N", out parsed)) return null;
            var snapshot = MemoryCache.Default.Get(CachePrefix + parsed.ToString("N")) as string[];
            return snapshot == null ? null : (string[])snapshot.Clone();
        }
    }
}
