using System;
using System.IO;
using System.Web;
using System.Web.Hosting;
using System.Web.Mvc;

namespace GTX.Controllers
{
    [AllowAnonymous]
    public class InventoryImagesController : Controller
    {
        [HttpGet]
        public ActionResult Get(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return HttpNotFound();
            }

            var relativePath = NormalizeRelative(path);
            if (string.IsNullOrWhiteSpace(relativePath))
            {
                return HttpNotFound();
            }

            // New location: ..\Pictures
            var resolved = ResolveFile(GetPicturesRoot(), relativePath);
            if (!string.IsNullOrWhiteSpace(resolved) && System.IO.File.Exists(resolved))
            {
                return File(resolved, MimeMapping.GetMimeMapping(resolved));
            }

            // Legacy fallback while migration is in progress.
            resolved = ResolveFile(GetLegacyInventoryRoot(), relativePath);
            if (!string.IsNullOrWhiteSpace(resolved) && System.IO.File.Exists(resolved))
            {
                return File(resolved, MimeMapping.GetMimeMapping(resolved));
            }

            return HttpNotFound();
        }

        private static string GetPicturesRoot()
        {
            var appRoot = HostingEnvironment.MapPath("~") ?? AppDomain.CurrentDomain.BaseDirectory;
            return Path.GetFullPath(Path.Combine(appRoot, "..", "Pictures"));
        }

        private static string GetLegacyInventoryRoot()
        {
            var appRoot = HostingEnvironment.MapPath("~") ?? AppDomain.CurrentDomain.BaseDirectory;
            return Path.GetFullPath(Path.Combine(appRoot, "GTXImages", "Inventory"));
        }

        private static string NormalizeRelative(string path)
        {
            return (path ?? string.Empty)
                .Trim()
                .Replace('\\', '/')
                .TrimStart('/');
        }

        private static string ResolveFile(string root, string relativePath)
        {
            if (string.IsNullOrWhiteSpace(root) || string.IsNullOrWhiteSpace(relativePath))
            {
                return null;
            }

            var combined = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
            var fullPath = Path.GetFullPath(combined);
            var rootWithSeparator = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;

            if (!fullPath.Equals(root, StringComparison.OrdinalIgnoreCase) &&
                !fullPath.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return fullPath;
        }
    }
}
