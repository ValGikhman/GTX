using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Web;
using System.Xml.Linq;

public static class SitemapWriter
{
    private const string BaseUrl = "https://usedcarscincinnati.com";
    private const string InventoryXmlVirtual = "~/App_Data/Inventory/Current/GTX-inventory.xml";
    private const string OutputVirtual = "~/sitemap.xml";

    public static void Write()
    {
        var ctx = HttpContext.Current;
        if (ctx == null) return;

        var invPath = ctx.Server.MapPath(InventoryXmlVirtual);
        var outPath = ctx.Server.MapPath(OutputVirtual);

        if (!File.Exists(invPath)) return;

        var invLastWriteUtc = File.GetLastWriteTimeUtc(invPath);
        var lastmod = invLastWriteUtc.ToString("yyyy-MM-dd");

        var inv = XDocument.Load(invPath);

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

        var vehicles = inv.Root.Elements("vehicle");

        foreach (var v in vehicles)
        {
            var stock = ((string)v.Element("Stock"))?.Trim();
            if (string.IsNullOrWhiteSpace(stock)) continue;

            var loc = $"{BaseUrl}/Inventory/Details?stock={Uri.EscapeDataString(stock)}";
            urlset.Add(Url(ns, loc, lastmod, "daily", "0.9"));
        }

        var sitemap = new XDocument(new XDeclaration("1.0", "UTF-8", "yes"), urlset);

        // Ensure folder exists and write
        File.WriteAllText(outPath, sitemap.ToString(SaveOptions.DisableFormatting), new UTF8Encoding(false));
    }

    private static XElement Url(XNamespace ns, string loc, string lastmod, string changefreq, string priority) =>
        new XElement(ns + "url",
            new XElement(ns + "loc", loc),
            new XElement(ns + "lastmod", lastmod),
            new XElement(ns + "changefreq", changefreq),
            new XElement(ns + "priority", priority)
        );
}
