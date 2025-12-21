using System;
using System.Linq;
using System.Text;
using System.Web.Mvc;
using System.Xml.Linq;

public class SitemapController : Controller
{
    private const string BaseUrl = "https://usedcarscincinnati.com";
    private const string InventoryXmlPath = "~/App_Data/Inventory/Current/GTX-inventory.xml";

    [Route("sitemap.xml")]
    public ActionResult Index()
    {
        var path = Server.MapPath(InventoryXmlPath);
        if (string.IsNullOrWhiteSpace(path) || !System.IO.File.Exists(path))
            return HttpNotFound();

        // If the file is rebuilt daily, this is a great lastmod.
        var inventoryLastWriteUtc = System.IO.File.GetLastWriteTimeUtc(path);
        var lastmod = inventoryLastWriteUtc.ToString("yyyy-MM-dd");

        XDocument inv;
        try
        {
            inv = XDocument.Load(path);
        }
        catch
        {
            // If XML is temporarily mid-write or malformed, fail safely.
            return new HttpStatusCodeResult(503, "Inventory XML not available");
        }

        XNamespace ns = "http://www.sitemaps.org/schemas/sitemap/0.9";

        var urlset = new XElement(ns + "urlset",
            // Core pages
            Url(ns, $"{BaseUrl}/", lastmod, "daily", "1.0"),
            Url(ns, $"{BaseUrl}/Inventory/All", lastmod, "daily", "0.95"),

            // Categories (keep what you actually serve)
            Url(ns, $"{BaseUrl}/Inventory/Suvs", lastmod, "daily", "0.8"),
            Url(ns, $"{BaseUrl}/Inventory/Trucks", lastmod, "daily", "0.8"),
            Url(ns, $"{BaseUrl}/Inventory/Vans", lastmod, "daily", "0.75"),
            Url(ns, $"{BaseUrl}/Inventory/Hatchbacks", lastmod, "weekly", "0.6"),
            Url(ns, $"{BaseUrl}/Inventory/Coupes", lastmod, "weekly", "0.6"),
            Url(ns, $"{BaseUrl}/Inventory/Sedans", lastmod, "daily", "0.75"),
            Url(ns, $"{BaseUrl}/Inventory/Convertibles", lastmod, "weekly", "0.55"),

            // Trust pages
            Url(ns, $"{BaseUrl}/Home/About", lastmod, "monthly", "0.5"),
            Url(ns, $"{BaseUrl}/Home/Testimonials", lastmod, "monthly", "0.5"),
            Url(ns, $"{BaseUrl}/Home/Staff", lastmod, "monthly", "0.4"),
            Url(ns, $"{BaseUrl}/Home/Contact", lastmod, "yearly", "0.4"),
            Url(ns, $"{BaseUrl}/Home/Application", lastmod, "monthly", "0.6")
        );

        // Vehicle pages: ONLY those marked SetToUpload=Y
        var vehiclesToShow = inv
            .Descendants("Vehicle")
            .Where(v => string.Equals(((string)v.Element("SetToUpload"))?.Trim(), "Y", StringComparison.OrdinalIgnoreCase));

        foreach (var v in vehiclesToShow)
        {
            var stock = ((string)v.Element("Stock"))?.Trim();
            if (string.IsNullOrWhiteSpace(stock))
                continue;

            var loc = $"{BaseUrl}/Inventory/Details?stock={Uri.EscapeDataString(stock)}";
            urlset.Add(Url(ns, loc, lastmod, "daily", "0.9"));
        }

        var sitemap = new XDocument(new XDeclaration("1.0", "UTF-8", "yes"), urlset);

        // Optional but nice: enable browser/proxy caching for a few hours
        Response.Cache.SetCacheability(System.Web.HttpCacheability.Public);
        Response.Cache.SetMaxAge(TimeSpan.FromHours(6));

        return Content(sitemap.ToString(SaveOptions.DisableFormatting), "application/xml", Encoding.UTF8);
    }

    private static XElement Url(XNamespace ns, string loc, string lastmod, string changefreq, string priority) =>
        new XElement(ns + "url",
            new XElement(ns + "loc", loc),
            new XElement(ns + "lastmod", lastmod),
            new XElement(ns + "changefreq", changefreq),
            new XElement(ns + "priority", priority)
        );
}
