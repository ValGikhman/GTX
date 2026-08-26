using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Configuration;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Web;

namespace GTX.Helpers {
    public static class InventoryImageSettings
    {
        private const string DefaultNativeBaseUrl = "https://photos.usedcarscincinnati.com/Images";
        private const string DefaultCloudflareBaseUrl = "https://pub-879e4994e6a64354b38b0729cd0c184c.r2.dev";

        public static bool CloudflareEnabled
        {
            get
            {
                bool enabled;
                return bool.TryParse(ConfigurationManager.AppSettings["Cloudflare"], out enabled) && enabled;
            }
        }

        public static string BaseUrl
        {
            get
            {
                var key = CloudflareEnabled ? "Images:CloudflareBaseUrl" : "Images:NativeBaseUrl";
                var fallback = CloudflareEnabled ? DefaultCloudflareBaseUrl : DefaultNativeBaseUrl;
                var configured = ConfigurationManager.AppSettings[key];
                return (string.IsNullOrWhiteSpace(configured) ? fallback : configured).TrimEnd('/');
            }
        }

        public static string PlaceholderUrl
        {
            get
            {
                var configured = ConfigurationManager.AppSettings["Images:CloudflareBaseUrl"];
                var baseUrl = string.IsNullOrWhiteSpace(configured) ? DefaultCloudflareBaseUrl : configured;
                return baseUrl.TrimEnd('/') + "/no-image-1.jpg";
            }
        }
    }

    public static class SiteImageUrl
    {
        private const string CdnFolder = "SiteImages";

        public static string Build(string path)
        {
            var value = (path ?? string.Empty).Trim().Replace('\\', '/');
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            Uri absoluteUri;
            if (Uri.TryCreate(value, UriKind.Absolute, out absoluteUri))
            {
                var cdnRoot = new Uri(InventoryImageSettings.BaseUrl + "/", UriKind.Absolute);
                if (!string.Equals(absoluteUri.Host, cdnRoot.Host, StringComparison.OrdinalIgnoreCase))
                {
                    return value;
                }

                value = absoluteUri.AbsolutePath;
            }

            value = value.TrimStart('~', '/');
            if (value.StartsWith(CdnFolder + "/", StringComparison.OrdinalIgnoreCase))
            {
                value = value.Substring(CdnFolder.Length + 1);
            }

            var encodedPath = string.Join(
                "/",
                value.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(Uri.EscapeDataString));

            return InventoryImageSettings.BaseUrl + "/" + CdnFolder + "/" + encodedPath;
        }
    }

    public enum InventoryImageVariant
    {
        Grid,
        Small,
        Card,
        Detail
    }

    public static class InventoryImageUrl
    {
        private const int ImageQuality = 80;

        public static string Build(string source, string stock, InventoryImageVariant variant)
        {
            var path = NormalizePath(source);
            if (string.IsNullOrWhiteSpace(path))
            {
                path = "no-image-1.jpg";
            }

            if (IsExternalUrl(path))
            {
                return path;
            }

            var normalizedStock = (stock ?? string.Empty).Trim();
            if (!IsPlaceholder(path) &&
                !string.IsNullOrWhiteSpace(normalizedStock) &&
                !path.StartsWith(normalizedStock + "/", StringComparison.OrdinalIgnoreCase))
            {
                path = normalizedStock + "/" + path;
            }

            var encodedPath = string.Join(
                "/",
                path.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(Uri.EscapeDataString));

            int width;
            int height;
            GetDimensions(variant, out width, out height);

            if (InventoryImageSettings.CloudflareEnabled)
            {
                return string.Format(
                    CultureInfo.InvariantCulture,
                    "{0}/cdn-cgi/image/width={1},height={2},quality={3},fit=scale-down,format=auto/{4}",
                    InventoryImageSettings.BaseUrl,
                    width,
                    height,
                    ImageQuality,
                    encodedPath);
            }

            return "/InventoryImages/Get?path=" + HttpUtility.UrlEncode(path) + "&w=" + width.ToString(CultureInfo.InvariantCulture);
        }

        private static string NormalizePath(string source)
        {
            var value = (source ?? string.Empty).Trim().Replace('\\', '/');
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            Uri absoluteUri;
            if (Uri.TryCreate(value, UriKind.Absolute, out absoluteUri))
            {
                Uri baseUri;
                if (!Uri.TryCreate(InventoryImageSettings.BaseUrl + "/", UriKind.Absolute, out baseUri) ||
                    !string.Equals(absoluteUri.Host, baseUri.Host, StringComparison.OrdinalIgnoreCase))
                {
                    return value;
                }

                value = absoluteUri.AbsolutePath.TrimStart('/');
                var transformMarker = value.IndexOf("cdn-cgi/image/", StringComparison.OrdinalIgnoreCase);
                if (transformMarker >= 0)
                {
                    var optionsEnd = value.IndexOf('/', transformMarker + "cdn-cgi/image/".Length);
                    value = optionsEnd >= 0 ? value.Substring(optionsEnd + 1) : string.Empty;
                }
            }

            if (value.IndexOf("InventoryImages/Get", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                var queryIndex = value.IndexOf('?');
                if (queryIndex >= 0 && queryIndex < value.Length - 1)
                {
                    var query = HttpUtility.ParseQueryString(value.Substring(queryIndex));
                    value = HttpUtility.UrlDecode(query["path"] ?? string.Empty);
                }
            }

            value = (value ?? string.Empty).Trim().Replace('\\', '/').TrimStart('/');
            foreach (var prefix in new[] { "SiteImages/Inventory/", "Pictures/", "Images/" })
            {
                if (value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    value = value.Substring(prefix.Length);
                    break;
                }
            }

            return value.Trim('/');
        }

        private static bool IsExternalUrl(string value)
        {
            return value.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                   value.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsPlaceholder(string value)
        {
            return value.IndexOf("no-image", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void GetDimensions(InventoryImageVariant variant, out int width, out int height)
        {
            switch (variant)
            {
                case InventoryImageVariant.Grid:
                    width = 88;
                    height = 66;
                    return;
                case InventoryImageVariant.Small:
                    width = 400;
                    height = 300;
                    return;
                case InventoryImageVariant.Card:
                    width = 600;
                    height = 450;
                    return;
                default:
                    width = 800;
                    height = 600;
                    return;
            }
        }
    }

    public static class EnumHelper<T>
    {

        #region Public Methods

        public static string GetDisplayValue(T value)
        {
            var fieldInfo = value.GetType().GetField(value.ToString());

            var descriptionAttributes = fieldInfo.GetCustomAttributes(
                typeof(DisplayAttribute), false) as DisplayAttribute[];

            if (descriptionAttributes == null) return string.Empty;
            return (descriptionAttributes.Length > 0) ? descriptionAttributes[0].Name : value.ToString();
        }

        public static IList<string> GetDisplayValues(Enum value)
        {
            return GetNames(value).Select(obj => GetDisplayValue(Parse(obj))).ToList();
        }

        public static IList<string> GetNames(Enum value)
        {
            return value.GetType().GetFields(BindingFlags.Static | BindingFlags.Public).Select(fi => fi.Name).ToList();
        }

        public static IList<T> GetValues(Enum value)
        {
            var enumValues = new List<T>();

            foreach (FieldInfo fi in value.GetType().GetFields(BindingFlags.Static | BindingFlags.Public))
            {
                enumValues.Add((T)Enum.Parse(value.GetType(), fi.Name, false));
            }
            return enumValues;
        }

        public static T Parse(String value)
        {
            return (T)Enum.Parse(typeof(T), value, true);
        }

        #endregion Public Methods
    }
    public static class I18n
    {
        // Returns a STRING (safe for ActionLink, attributes, etc.)
        public static string R(string key)
        {
            var v = HttpContext.GetGlobalResourceObject("Site", key);
            return (v ?? key).ToString();
        }

        // ✅ Format a resource string using current culture
        public static string F(string key, params object[] args)
        {
            var template = R(key);

            // Ensure numbers/dates format correctly per current culture
            return string.Format(CultureInfo.CurrentUICulture, template, args);
        }

        // Optional: current language for quick branching
        public static string Lang =>
            CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
    }
}
