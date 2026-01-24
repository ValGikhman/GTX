using System;
using System.Linq;
using System.Runtime.Caching;

public static class AppCache
{
    private static readonly ObjectCache Cache = MemoryCache.Default;

    public static T GetOrCreate<T>(string key, Func<T> factory, int minutes = 10) where T : class
    {
        var existing = Cache.Get(key) as T;
        if (existing != null) return existing;

        // lock per key so multiple requests don't rebuild at once
        var lockObj = Cache.AddOrGetExisting(key + ":lock", new object(), new CacheItemPolicy { AbsoluteExpiration = DateTimeOffset.UtcNow.AddMinutes(minutes) }) as object ?? new object();

        lock (lockObj)
        {
            existing = Cache.Get(key) as T;
            if (existing != null) return existing;

            var value = factory();
            if (value == null) return null;

            Cache.Set(key, value, new CacheItemPolicy
            {
                AbsoluteExpiration = DateTimeOffset.UtcNow.AddMinutes(minutes)
            });

            return value;
        }
    }

    public static void Remove(string key) => Cache.Remove(key);

    public static void ClearAll()
    {
        foreach (var item in Cache.ToList())
        {
            Cache.Remove(item.Key);
        }
    }
}
