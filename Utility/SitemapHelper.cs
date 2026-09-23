using System;
using System.IO;
using System.Linq;
using System.Text;
using Common;
using System.Xml.Linq;
using System.Text.RegularExpressions;
using Services;

public static class SitemapWriter
{
    private const string BaseUrl = "https://usedcarscincinnati.com";
    private const string OutputVirtual = "~/sitemap.xml";
    private static readonly object WriteLock = new object();

    public static void Write(IInventoryService inventoryService)
    {
        if (inventoryService == null) throw new ArgumentNullException(nameof(inventoryService));
        // Read inside the lock so concurrent updates cannot publish an older snapshot last.
        lock (WriteLock)
        {
            var inventory = inventoryService.GetInventory(includeHiddenInventory: false, includeDataOneContent: false);
            var sitemap = Build(inventory.vehicles, DateTime.UtcNow);
            var outPath = System.Web.Hosting.HostingEnvironment.MapPath(OutputVirtual);
            var tmpPath = outPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(tmpPath, sitemap.ToString(SaveOptions.DisableFormatting), new UTF8Encoding(false));
                if (File.Exists(outPath)) File.Replace(tmpPath, outPath, null);
                else File.Move(tmpPath, outPath);
                System.Runtime.Caching.MemoryCache.Default.Remove("SitemapViewer");
            }
            finally
            {
                if (File.Exists(tmpPath)) File.Delete(tmpPath);
            }
        }
    }

    public static XDocument Build(System.Collections.Generic.IEnumerable<GTXDTO> vehicles, DateTime updatedUtc)
    {
        if (vehicles == null) throw new ArgumentNullException(nameof(vehicles));
        var lastmod = updatedUtc.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ");
        XNamespace ns = "http://www.sitemaps.org/schemas/sitemap/0.9";

        var urlset = new XElement(ns + "urlset",
            Url(ns, $"{BaseUrl}/", lastmod, "daily", "1.0"),
            Url(ns, $"{BaseUrl}/Inventory/All", lastmod, "daily", "0.95"),
            Url(ns, $"{BaseUrl}/Inventory/Suvs", lastmod, "daily", "0.8"),
            Url(ns, $"{BaseUrl}/Inventory/Trucks", lastmod, "daily", "0.8"),
            Url(ns, $"{BaseUrl}/Inventory/Vans", lastmod, "daily", "0.75"),
            Url(ns, $"{BaseUrl}/Inventory/Sedans", lastmod, "daily", "0.75"),
            Url(ns, $"{BaseUrl}/Home/About", lastmod, "monthly", "0.5"),
            Url(ns, $"{BaseUrl}/Home/Contact", lastmod, "yearly", "0.4"),
            Url(ns, $"{BaseUrl}/Home/Application", lastmod, "monthly", "0.6")
        );

        foreach (var v in vehicles)
        {
            var stock = v.Stock ?? string.Empty;
            var year = v.Year.ToString();
            var make = v.Make ?? string.Empty;
            var model = v.Model ?? string.Empty;
            var vehicleStyle = v.VehicleStyle ?? string.Empty;

            if (string.IsNullOrWhiteSpace(stock)) continue;
            if (!string.Equals(v.SetToUpload?.Trim(), "Y", StringComparison.OrdinalIgnoreCase)) continue;
            string SlugPart(string s) => Regex.Replace((s ?? "").Trim(), @"[\s/]+", "-");
            var loc = $"{BaseUrl}/Inventory/{Regex.Replace(string.Join("-", new[] { year, SlugPart(make), SlugPart(model), SlugPart(vehicleStyle), stock }.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim())), @"-+", "-").Trim('-')}";

            urlset.Add(Url(ns, loc, lastmod, "daily", "0.9"));
        }

        return new XDocument(new XDeclaration("1.0", "UTF-8", "yes"), urlset);
    }

    private static XElement Url(XNamespace ns, string loc, string lastmod, string changefreq, string priority) =>
        new XElement(ns + "url",
            new XElement(ns + "loc", loc),
            new XElement(ns + "lastmod", lastmod),
            new XElement(ns + "changefreq", changefreq),
            new XElement(ns + "priority", priority)
        );
}
