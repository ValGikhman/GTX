using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
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
        public ActionResult Get(string path, int? width = null)
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
                return CreateImageResult(resolved, width);
            }

            // Legacy fallback while migration is in progress.
            resolved = ResolveFile(GetLegacyInventoryRoot(), relativePath);
            if (!string.IsNullOrWhiteSpace(resolved) && System.IO.File.Exists(resolved))
            {
                return CreateImageResult(resolved, width);
            }

            return HttpNotFound();
        }

        private ActionResult CreateImageResult(string fullPath, int? width)
        {
            var mimeType = MimeMapping.GetMimeMapping(fullPath);
            if (!width.HasValue || width.Value <= 0)
            {
                return File(fullPath, mimeType);
            }

            try
            {
                var resized = TryResize(fullPath, mimeType, width.Value);
                return resized == null ? File(fullPath, mimeType) : File(resized, mimeType);
            }
            catch
            {
                // If resize fails for any reason, return the original image safely.
                return File(fullPath, mimeType);
            }
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

        private static byte[] TryResize(string fullPath, string mimeType, int requestedWidth)
        {
            if (!CanResizeMimeType(mimeType))
            {
                return null;
            }

            var targetWidth = Math.Max(1, Math.Min(requestedWidth, 4096));
            using (var source = Image.FromFile(fullPath))
            {
                if (source.Width <= targetWidth)
                {
                    return null;
                }

                var targetHeight = Math.Max(1, (int)Math.Round(source.Height * (targetWidth / (double)source.Width)));
                using (var resized = new Bitmap(targetWidth, targetHeight))
                {
                    if (source.HorizontalResolution > 0 && source.VerticalResolution > 0)
                    {
                        resized.SetResolution(source.HorizontalResolution, source.VerticalResolution);
                    }

                    using (var graphics = Graphics.FromImage(resized))
                    {
                        graphics.CompositingQuality = CompositingQuality.HighQuality;
                        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        graphics.SmoothingMode = SmoothingMode.HighQuality;
                        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                        graphics.DrawImage(source, 0, 0, targetWidth, targetHeight);
                    }

                    using (var stream = new MemoryStream())
                    {
                        resized.Save(stream, GetImageFormat(mimeType));
                        return stream.ToArray();
                    }
                }
            }
        }

        private static bool CanResizeMimeType(string mimeType)
        {
            if (string.IsNullOrWhiteSpace(mimeType))
            {
                return false;
            }

            return mimeType.Equals("image/jpeg", StringComparison.OrdinalIgnoreCase) ||
                   mimeType.Equals("image/jpg", StringComparison.OrdinalIgnoreCase) ||
                   mimeType.Equals("image/png", StringComparison.OrdinalIgnoreCase) ||
                   mimeType.Equals("image/gif", StringComparison.OrdinalIgnoreCase) ||
                   mimeType.Equals("image/bmp", StringComparison.OrdinalIgnoreCase);
        }

        private static ImageFormat GetImageFormat(string mimeType)
        {
            if (mimeType.Equals("image/jpeg", StringComparison.OrdinalIgnoreCase) ||
                mimeType.Equals("image/jpg", StringComparison.OrdinalIgnoreCase))
            {
                return ImageFormat.Jpeg;
            }

            if (mimeType.Equals("image/gif", StringComparison.OrdinalIgnoreCase))
            {
                return ImageFormat.Gif;
            }

            if (mimeType.Equals("image/bmp", StringComparison.OrdinalIgnoreCase))
            {
                return ImageFormat.Bmp;
            }

            return ImageFormat.Png;
        }
    }
}
