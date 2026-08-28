using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web.Hosting;

namespace GTX.Helpers
{
    internal sealed class CloudflareR2BucketStats
    {
        public long TotalObjects { get; set; }
        public long ImageObjects { get; set; }
        public long OtherObjects { get; set; }
        public long TotalBytes { get; set; }
        public long ImageBytes { get; set; }
        public int StockFolders { get; set; }
    }

    internal static class CloudflareR2Storage
    {
        private const string DefaultBucketName = "gtx";
        private static readonly Lazy<IAmazonS3> Client = new Lazy<IAmazonS3>(CreateClient);
        private static readonly HashSet<string> ImageExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".avif", ".bmp", ".gif", ".heic", ".heif", ".jpeg", ".jpg", ".png", ".tif", ".tiff", ".webp"
        };

        private static string BucketName => GetSetting("Cloudflare:R2:BucketName", DefaultBucketName);

        public static async Task<bool> ExistsAsync(string stock, string file)
        {
            try
            {
                await Client.Value.GetObjectMetadataAsync(new GetObjectMetadataRequest
                {
                    BucketName = BucketName,
                    Key = BuildKey(stock, file)
                });
                return true;
            }
            catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                return false;
            }
        }

        public static async Task<byte[]> ReadAsync(string stock, string file)
        {
            using (var response = await Client.Value.GetObjectAsync(new GetObjectRequest
            {
                BucketName = BucketName,
                Key = BuildKey(stock, file)
            }))
            using (var output = new MemoryStream())
            {
                await response.ResponseStream.CopyToAsync(output);
                return output.ToArray();
            }
        }

        public static async Task WriteAsync(string stock, string file, byte[] content, string contentType)
        {
            if (content == null || content.Length == 0)
            {
                throw new ArgumentException("Image content is required.", nameof(content));
            }

            using (var input = new MemoryStream(content, false))
            {
                var request = new PutObjectRequest
                {
                    BucketName = BucketName,
                    Key = BuildKey(stock, file),
                    InputStream = input,
                    ContentType = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType,
                    AutoCloseStream = false,
                    DisablePayloadSigning = true,
                    UseChunkEncoding = false
                };
                request.Headers.CacheControl = "public, max-age=0, must-revalidate";
                await Client.Value.PutObjectAsync(request);
            }
        }

        public static async Task WriteKeyAsync(string key, Stream content, string contentType)
        {
            if (content == null)
            {
                throw new ArgumentNullException(nameof(content));
            }

            var normalizedKey = NormalizeKey(key);
            if (content.CanSeek)
            {
                content.Position = 0;
            }

            var request = new PutObjectRequest
            {
                BucketName = BucketName,
                Key = normalizedKey,
                InputStream = content,
                ContentType = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType,
                AutoCloseStream = false,
                DisablePayloadSigning = true,
                UseChunkEncoding = false
            };
            request.Headers.CacheControl = "public, max-age=31536000, immutable";
            await Client.Value.PutObjectAsync(request);
        }

        public static Task DeleteKeyAsync(string key)
        {
            return Client.Value.DeleteObjectAsync(new DeleteObjectRequest
            {
                BucketName = BucketName,
                Key = NormalizeKey(key)
            });
        }

        public static Task DeleteAsync(string stock, string file)
        {
            return Client.Value.DeleteObjectAsync(new DeleteObjectRequest
            {
                BucketName = BucketName,
                Key = BuildKey(stock, file)
            });
        }

        public static async Task<CloudflareR2BucketStats> GetBucketStatsAsync()
        {
            var stats = new CloudflareR2BucketStats();
            var stockFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string continuationToken = null;

            do
            {
                var response = await Client.Value.ListObjectsV2Async(new ListObjectsV2Request
                {
                    BucketName = BucketName,
                    ContinuationToken = continuationToken,
                    MaxKeys = 1000
                });

                foreach (var item in response.S3Objects ?? new List<S3Object>())
                {
                    stats.TotalObjects++;
                    stats.TotalBytes += item.Size;

                    if (ImageExtensions.Contains(Path.GetExtension(item.Key ?? string.Empty)))
                    {
                        stats.ImageObjects++;
                        stats.ImageBytes += item.Size;
                    }

                    var slashIndex = (item.Key ?? string.Empty).IndexOf('/');
                    if (slashIndex > 0)
                    {
                        stockFolders.Add(item.Key.Substring(0, slashIndex));
                    }
                }

                continuationToken = response.IsTruncated ? response.NextContinuationToken : null;
            }
            while (!string.IsNullOrWhiteSpace(continuationToken));

            stats.OtherObjects = stats.TotalObjects - stats.ImageObjects;
            stats.StockFolders = stockFolders.Count;
            return stats;
        }

        public static async Task DeleteManyAsync(string stock, IEnumerable<string> files)
        {
            var keys = (files ?? Enumerable.Empty<string>())
                .Where(file => !string.IsNullOrWhiteSpace(file))
                .Select(file => new KeyVersion { Key = BuildKey(stock, file) })
                .GroupBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToList();

            foreach (var batch in Batch(keys, 1000))
            {
                await Client.Value.DeleteObjectsAsync(new DeleteObjectsRequest
                {
                    BucketName = BucketName,
                    Objects = batch,
                    Quiet = true
                });
            }
        }

        private static IAmazonS3 CreateClient()
        {
            var accessKey = ConfigurationManager.AppSettings["Cloudflare:R2:AccessKeyId"];
            var secretKey = ConfigurationManager.AppSettings["Cloudflare:R2:SecretAccessKey"];
            var endpoint = ConfigurationManager.AppSettings["Cloudflare:R2:Endpoint"];

            if (string.IsNullOrWhiteSpace(accessKey) ||
                string.IsNullOrWhiteSpace(secretKey) ||
                string.IsNullOrWhiteSpace(endpoint))
            {
                LoadMigrationCredentials(ref accessKey, ref secretKey, ref endpoint);
            }

            if (string.IsNullOrWhiteSpace(accessKey) ||
                string.IsNullOrWhiteSpace(secretKey) ||
                string.IsNullOrWhiteSpace(endpoint))
            {
                throw new ConfigurationErrorsException(
                    "Cloudflare R2 credentials are missing. Configure Cloudflare:R2:AccessKeyId, " +
                    "Cloudflare:R2:SecretAccessKey, and Cloudflare:R2:Endpoint.");
            }

            return new AmazonS3Client(
                new BasicAWSCredentials(accessKey.Trim(), secretKey.Trim()),
                new AmazonS3Config
                {
                    ServiceURL = endpoint.Trim().TrimEnd('/'),
                    AuthenticationRegion = "auto",
                    ForcePathStyle = true
                });
        }

        private static void LoadMigrationCredentials(ref string accessKey, ref string secretKey, ref string endpoint)
        {
            var appRoot = HostingEnvironment.MapPath("~") ?? AppDomain.CurrentDomain.BaseDirectory;
            var candidates = new[]
            {
                Path.Combine(appRoot, "App_Data", "R2Credentials.txt"),
                Path.Combine(appRoot, "Your API Token.txt"),
                Path.Combine(Environment.CurrentDirectory, "Your API Token.txt")
            };
            var credentialFile = candidates.FirstOrDefault(File.Exists);
            if (string.IsNullOrWhiteSpace(credentialFile))
            {
                return;
            }

            var lines = File.ReadAllLines(credentialFile);
            accessKey = string.IsNullOrWhiteSpace(accessKey) ? GetValueAfterLabel(lines, "Access Key ID") : accessKey;
            secretKey = string.IsNullOrWhiteSpace(secretKey) ? GetValueAfterLabel(lines, "Secret Access Key") : secretKey;
            endpoint = string.IsNullOrWhiteSpace(endpoint) ? GetValueAfterLabel(lines, "S3 API endpoint") : endpoint;
        }

        private static string GetValueAfterLabel(string[] lines, string label)
        {
            for (var index = 0; index < lines.Length - 1; index++)
            {
                if (string.Equals((lines[index] ?? string.Empty).Trim(), label, StringComparison.OrdinalIgnoreCase))
                {
                    return (lines[index + 1] ?? string.Empty).Trim();
                }
            }

            return null;
        }

        private static string BuildKey(string stock, string file)
        {
            var normalizedStock = (stock ?? string.Empty).Trim().Trim('/', '\\');
            var normalizedFile = Path.GetFileName((file ?? string.Empty).Replace('\\', '/'));
            if (string.IsNullOrWhiteSpace(normalizedStock) || string.IsNullOrWhiteSpace(normalizedFile) ||
                normalizedStock.Contains("/") || normalizedStock.Contains("\\") || normalizedStock.Contains(".."))
            {
                throw new ArgumentException("A valid stock and image filename are required.");
            }

            return normalizedStock + "/" + normalizedFile;
        }

        private static string NormalizeKey(string key)
        {
            var normalized = (key ?? string.Empty).Trim().Replace('\\', '/').Trim('/');
            if (string.IsNullOrWhiteSpace(normalized) ||
                normalized.Split('/').Any(part => string.IsNullOrWhiteSpace(part) || part == "." || part == ".."))
            {
                throw new ArgumentException("A valid R2 object key is required.", nameof(key));
            }

            return normalized;
        }

        private static string GetSetting(string key, string fallback)
        {
            var configured = ConfigurationManager.AppSettings[key];
            return string.IsNullOrWhiteSpace(configured) ? fallback : configured.Trim();
        }

        private static IEnumerable<List<KeyVersion>> Batch(List<KeyVersion> values, int batchSize)
        {
            for (var index = 0; index < values.Count; index += batchSize)
            {
                yield return values.GetRange(index, Math.Min(batchSize, values.Count - index));
            }
        }
    }
}
