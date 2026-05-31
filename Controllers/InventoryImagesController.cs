using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Web;
using System.Web.Hosting;
using System.Web.Mvc;

namespace GTX.Controllers
{
    [AllowAnonymous]
    public class InventoryImagesController : Controller
    {
        private static readonly TimeSpan ImageCacheDuration = TimeSpan.FromDays(30);

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
            var fileInfo = new FileInfo(fullPath);
            var mimeType = MimeMapping.GetMimeMapping(fullPath);
            var effectiveWidth = !width.HasValue || width.Value <= 0
                ? 0
                : Math.Max(1, Math.Min(width.Value, 4096));

            var etag = BuildEtag(fileInfo, effectiveWidth);
            var lastModifiedUtc = fileInfo.LastWriteTimeUtc;

            if (IsNotModified(etag, lastModifiedUtc))
            {
                ApplyCacheHeaders(etag, lastModifiedUtc);
                return new HttpStatusCodeResult(HttpStatusCode.NotModified);
            }

            ApplyCacheHeaders(etag, lastModifiedUtc);

            if (effectiveWidth <= 0)
            {
                return File(fullPath, mimeType);
            }

            try
            {
                var cachedResizedPath = TryGetOrCreateResizedPath(fullPath, mimeType, effectiveWidth, fileInfo.LastWriteTimeUtc);
                if (!string.IsNullOrWhiteSpace(cachedResizedPath))
                {
                    return File(cachedResizedPath, mimeType);
                }

                return File(fullPath, mimeType);
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

        private void ApplyCacheHeaders(string etag, DateTime lastModifiedUtc)
        {
            var cache = Response.Cache;
            cache.SetCacheability(HttpCacheability.Public);
            cache.SetMaxAge(ImageCacheDuration);
            cache.SetExpires(DateTime.UtcNow.Add(ImageCacheDuration));
            cache.SetValidUntilExpires(true);
            cache.SetLastModified(lastModifiedUtc);
            cache.SetETag(etag);
            cache.SetRevalidation(HttpCacheRevalidation.AllCaches);
        }

        private bool IsNotModified(string etag, DateTime lastModifiedUtc)
        {
            var ifNoneMatch = Request.Headers["If-None-Match"];
            if (!string.IsNullOrWhiteSpace(ifNoneMatch))
            {
                var tokens = ifNoneMatch.Split(',');
                foreach (var token in tokens)
                {
                    var candidate = (token ?? string.Empty).Trim();
                    if (candidate == "*" || string.Equals(candidate, etag, StringComparison.Ordinal))
                    {
                        return true;
                    }
                }

                return false;
            }

            var ifModifiedSince = Request.Headers["If-Modified-Since"];
            if (string.IsNullOrWhiteSpace(ifModifiedSince))
            {
                return false;
            }

            DateTime parsedDate;
            if (!DateTime.TryParse(ifModifiedSince, out parsedDate))
            {
                return false;
            }

            return lastModifiedUtc <= parsedDate.ToUniversalTime().AddSeconds(1);
        }

        private static string BuildEtag(FileInfo fileInfo, int width)
        {
            return $"\"{fileInfo.Length:x}-{fileInfo.LastWriteTimeUtc.Ticks:x}-{width:x}\"";
        }

        private static string GetImageCacheRoot()
        {
            var appRoot = HostingEnvironment.MapPath("~") ?? AppDomain.CurrentDomain.BaseDirectory;
            return Path.Combine(appRoot, "App_Data", "ImageCache");
        }

        private static string TryGetOrCreateResizedPath(string fullPath, string mimeType, int requestedWidth, DateTime sourceLastWriteUtc)
        {
            if (!CanResizeMimeType(mimeType))
            {
                return null;
            }

            var cacheRoot = GetImageCacheRoot();
            Directory.CreateDirectory(cacheRoot);

            var cachePath = BuildResizedCachePath(cacheRoot, fullPath, mimeType, requestedWidth, sourceLastWriteUtc);
            if (System.IO.File.Exists(cachePath))
            {
                return cachePath;
            }

            var resized = TryResize(fullPath, mimeType, requestedWidth);
            if (resized == null)
            {
                return null;
            }

            var tempPath = cachePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            System.IO.File.WriteAllBytes(tempPath, resized);

            try
            {
                if (System.IO.File.Exists(cachePath))
                {
                    return cachePath;
                }

                System.IO.File.Move(tempPath, cachePath);
                return cachePath;
            }
            catch (IOException)
            {
                if (System.IO.File.Exists(cachePath))
                {
                    return cachePath;
                }

                throw;
            }
            finally
            {
                if (System.IO.File.Exists(tempPath))
                {
                    try
                    {
                        System.IO.File.Delete(tempPath);
                    }
                    catch
                    {
                        // Best-effort cleanup.
                    }
                }
            }
        }

        private static string BuildResizedCachePath(string cacheRoot, string fullPath, string mimeType, int requestedWidth, DateTime sourceLastWriteUtc)
        {
            var fingerprint = $"{fullPath}|{sourceLastWriteUtc.Ticks}|{requestedWidth}|{mimeType}";
            using (var sha256 = SHA256.Create())
            {
                var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(fingerprint));
                var hashText = BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
                return Path.Combine(cacheRoot, $"{hashText}-{requestedWidth}{GetCacheExtension(mimeType)}");
            }
        }

        private static string GetCacheExtension(string mimeType)
        {
            if (mimeType.Equals("image/jpeg", StringComparison.OrdinalIgnoreCase) ||
                mimeType.Equals("image/jpg", StringComparison.OrdinalIgnoreCase))
            {
                return ".jpg";
            }

            if (mimeType.Equals("image/gif", StringComparison.OrdinalIgnoreCase))
            {
                return ".gif";
            }

            if (mimeType.Equals("image/bmp", StringComparison.OrdinalIgnoreCase))
            {
                return ".bmp";
            }

            return ".png";
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
