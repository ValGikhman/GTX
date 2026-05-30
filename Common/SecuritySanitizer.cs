using System;
using System.Text.RegularExpressions;
using System.Web;

namespace GTX.Helpers
{
    public static class SecuritySanitizer
    {
        private static readonly Regex DangerousContainerTagsRegex = new Regex(
            @"<\s*(script|iframe|object|embed|style|link|meta|base|form|input|button|textarea|select)\b[^>]*>(.*?)<\s*/\s*\1\s*>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

        private static readonly Regex DangerousSelfClosingTagsRegex = new Regex(
            @"<\s*(script|iframe|object|embed|style|link|meta|base|form|input|button|textarea|select)\b[^>]*\/?\s*>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

        private static readonly Regex EventHandlerAttributeRegex = new Regex(
            @"\s+on[a-z0-9_-]+\s*=\s*(?:""[^""]*""|'[^']*'|[^\s>]+)",
            RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

        private static readonly Regex DangerousProtocolAttributeRegex = new Regex(
            @"\s+(href|src|xlink:href)\s*=\s*(['""])\s*(?:javascript|vbscript|data)\s*:[^'""]*\2",
            RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

        private static readonly Regex DangerousUnquotedProtocolAttributeRegex = new Regex(
            @"\s+(href|src|xlink:href)\s*=\s*(?:javascript|vbscript|data)\s*:[^\s>]+",
            RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

        private static readonly Regex DangerousStyleAttributeRegex = new Regex(
            @"\s+style\s*=\s*(['""])[^'""]*(?:expression\s*\(|javascript:|vbscript:|url\s*\(\s*['""]?\s*(?:javascript|vbscript|data)\s*:)[^'""]*\1",
            RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

        public static string SanitizeRichHtml(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return string.Empty;
            }

            var html = input.Trim().Replace("\0", string.Empty);

            html = DangerousContainerTagsRegex.Replace(html, string.Empty);
            html = DangerousSelfClosingTagsRegex.Replace(html, string.Empty);
            html = EventHandlerAttributeRegex.Replace(html, string.Empty);
            html = DangerousProtocolAttributeRegex.Replace(html, string.Empty);
            html = DangerousUnquotedProtocolAttributeRegex.Replace(html, string.Empty);
            html = DangerousStyleAttributeRegex.Replace(html, string.Empty);

            return html;
        }

        public static string SanitizeHttpOrRelativeUrl(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var input = value.Trim();

            if (input.StartsWith("~/", StringComparison.Ordinal))
            {
                return VirtualPathUtility.ToAbsolute(input);
            }

            if (input.StartsWith("/", StringComparison.Ordinal) &&
                !input.StartsWith("//", StringComparison.Ordinal))
            {
                return input;
            }

            if (Uri.TryCreate(input, UriKind.Absolute, out var uri) &&
                (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            {
                return uri.ToString();
            }

            return null;
        }
    }
}
