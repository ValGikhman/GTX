using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Mvc;
using System.IO;
using System.Web;
using Microsoft.Extensions.Caching.Memory;

public class ImageController : Controller
{
    // Simple in-memory cache (per app domain)
    private static readonly IMemoryCache Cache = new MemoryCache(new MemoryCacheOptions());

    // Shared HttpClient
    private static readonly HttpClient Http = new HttpClient();

    // /Image/Proxy?url=...
    public async Task<ActionResult> Proxy(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return new HttpStatusCodeResult(HttpStatusCode.BadRequest, "Missing url");

        // Use full url as cache key
        var key = "img:" + url;

        if (Cache.Get(key) is byte[] cachedBytes)
        {
            // Fire-and-forget refresh in background (SWR)
            _ = RefreshInBackground(url, key);

            SetBrowserCacheHeaders();
            return File(cachedBytes, GetMimeType(url));
        }

        // First time: fetch
        byte[] bytes;
        try
        {
            bytes = await Http.GetByteArrayAsync(url);
        }
        catch
        {
            // If remote fails, return 404 placeholder or just 404
            return HttpNotFound("Image not found");
        }

        var options = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(12)
        };

        Cache.Set(key, bytes, options);

        SetBrowserCacheHeaders();
        return File(bytes, GetMimeType(url));
    }

    private async Task RefreshInBackground(string url, string key)
    {
        try
        {
            var fresh = await Http.GetByteArrayAsync(url);

            var options = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(12)
            };

            Cache.Set(key, fresh, options);
        }
        catch
        {
            // Ignore refresh errors – serve stale cache next time
        }
    }

    private void SetBrowserCacheHeaders()
    {
        // Let browser cache it for 1 day
        Response.Cache.SetCacheability(HttpCacheability.Public);
        Response.Cache.SetMaxAge(TimeSpan.FromDays(1));
    }

    private string GetMimeType(string url)
    {
        var ext = Path.GetExtension(url).ToLowerInvariant();
        return ext switch
        {
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            _ => "image/jpeg"
        };
    }
}
