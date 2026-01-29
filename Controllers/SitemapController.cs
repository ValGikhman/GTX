using GTX.Models;
using Services;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Caching;
using System.Web.Mvc;
using System.Xml.Linq;

namespace GTX.Controllers
{
    public class SitemapController : BaseController
    {
        // small cache so you don’t parse 10k urls every request
        private static readonly ObjectCache Cache = MemoryCache.Default;
        private const string CacheKey = "SitemapViewer";

        public SitemapController(ISessionData sessionData, IInventoryService inventoryService, IVinDecoderService vinDecoderService, IEZ360Service eZ360Service, ILogService logService, IEmployeesService employeesService) :
            base(sessionData, inventoryService, vinDecoderService, eZ360Service, logService, employeesService)
        {
        }

        public ActionResult Index()
        {
            var cached = Cache.Get(CacheKey) as SitemapViewer;
            if (cached != null) return View("Index", cached);

            var physical = Server.MapPath("~/sitemap.xml");
            if (!System.IO.File.Exists(physical))
                return HttpNotFound("sitemap.xml not found at ~/sitemap.xml");

            var xdoc = XDocument.Load(physical);

            // sitemap namespace
            XNamespace ns = "http://www.sitemaps.org/schemas/sitemap/0.9";

            var urls = xdoc.Descendants(ns + "url")
                .Select(u => new SitemapModel
                {
                    Loc = (string)u.Element(ns + "loc"),
                    LastMod = ParseDate((string)u.Element(ns + "lastmod")),
                    ChangeFreq = (string)u.Element(ns + "changefreq"),
                    Priority = ParseDecimal((string)u.Element(ns + "priority"))
                })
                .Where(x => !string.IsNullOrWhiteSpace(x.Loc))
                .ToList();

            // group them
            var groups = BuildGroups(urls);

            var model = new SitemapViewer
            {
                SourcePath = "~/sitemap.xml",
                GeneratedUtc = DateTime.UtcNow,
                Groups = groups,
                TotalCount = urls.Count
            };

            Cache.Set(CacheKey, model, DateTimeOffset.UtcNow.AddMinutes(10));
            return View(model);
        }

        private static List<SitemapGroup> BuildGroups(List<SitemapModel> urls)
        {
            // inventory category pages you listed
            var inventoryCategoryPages = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "/Inventory/All", "/Inventory/Suvs", "/Inventory/Trucks", "/Inventory/Vans", "/Inventory/Sedans"
            };

            string Classify(string loc)
            {
                // normalize path only
                Uri uri;
                if (Uri.TryCreate(loc, UriKind.Absolute, out uri))
                {
                    var path = uri.AbsolutePath;

                    if (path == "/" || string.IsNullOrEmpty(path)) return "home";
                    if (path.StartsWith("/Home/", StringComparison.OrdinalIgnoreCase)) return "site-pages";

                    if (path.StartsWith("/Inventory/", StringComparison.OrdinalIgnoreCase))
                    {
                        if (inventoryCategoryPages.Contains(path)) return "inventory-categories";

                        // anything else under /Inventory/ is likely a vehicle detail page
                        return "inventory-vehicles";
                    }

                    return "other";
                }

                return "other";
            }

            string TitleFor(string key)
            {
                switch (key)
                {
                    case "home": return "Home";
                    case "inventory-categories": return "Inventory Category Pages";
                    case "inventory-vehicles": return "Vehicle Detail Pages";
                    case "site-pages": return "Site Pages";
                    default: return "Other";
                }
            }

            return urls
                .GroupBy(u => Classify(u.Loc))
                .OrderBy(g =>
                    g.Key == "home" ? 0 :
                    g.Key == "inventory-categories" ? 1 :
                    g.Key == "inventory-vehicles" ? 2 :
                    g.Key == "site-pages" ? 3 : 9)
                .Select(g => new SitemapGroup
                {
                    Key = g.Key,
                    Title = TitleFor(g.Key),
                    Items = g
                        .OrderByDescending(x => x.Priority ?? 0m)
                        .ThenByDescending(x => x.LastMod ?? DateTime.MinValue)
                        .ThenBy(x => x.Loc)
                        .ToList()
                })
                .ToList();
        }

        private static DateTime? ParseDate(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            // sitemap often uses ISO 8601 like 2026-01-28T15:15:00Z
            DateTime dt;
            if (DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out dt))
                return dt.ToUniversalTime();
            return null;
        }

        private static decimal? ParseDecimal(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            decimal d;
            if (decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out d))
                return d;
            return null;
        }
    }
}
