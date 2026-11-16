using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Caching;

namespace GTX.Common
{
    // Each search URL owns a stock snapshot; no visitor or tab has a global current search.
    public static class InventorySearchResults
    {
        private const string CachePrefix = "GTX:InventorySearchResults:";

        public static string Store(IEnumerable<string> stocks)
        {
            var snapshot = (stocks ?? Enumerable.Empty<string>())
                .Where(stock => !string.IsNullOrWhiteSpace(stock))
                .Select(stock => stock.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            // An empty search must stay empty after the redirect.
            var token = Guid.NewGuid().ToString("N");
            MemoryCache.Default.Set(CachePrefix + token, snapshot,
                DateTimeOffset.UtcNow.AddHours(1));
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
