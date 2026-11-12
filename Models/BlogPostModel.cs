using GTX.Helpers;
using Services;
using System;
using System.ComponentModel.DataAnnotations;
using System.Web.Mvc;

namespace GTX.Models
{
    public class BlogPostModel
    {
        public int Id { get; set; }

        [Required, StringLength(200)]
        public string Title { get; set; } = "";

        public string ArticleURL { get; set; }

        public string MediaURL { get; set; }

        public string SafeMediaUrl
        {
            get
            {
                var value = MediaURL?.Trim();
                var safeValue = SecuritySanitizer.SanitizeHttpOrRelativeUrl(value);
                if (safeValue == null) return null;
                // Keep escaped query values intact for signed media links.
                return value.StartsWith("~/", StringComparison.Ordinal) ? safeValue : value;
            }
        }

        public string VideoContentType
        {
            get
            {
                var safeUrl = SafeMediaUrl;
                if (string.IsNullOrWhiteSpace(safeUrl)) return null;

                var path = safeUrl.StartsWith("/", StringComparison.Ordinal)
                    ? safeUrl.Split(new[] { '?', '#' }, 2)[0]
                    : new Uri(safeUrl, UriKind.Absolute).AbsolutePath;

                if (path.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase)
                    || path.EndsWith(".m4v", StringComparison.OrdinalIgnoreCase)) return "video/mp4";
                if (path.EndsWith(".webm", StringComparison.OrdinalIgnoreCase)) return "video/webm";
                if (path.EndsWith(".ogv", StringComparison.OrdinalIgnoreCase)) return "video/ogg";
                return null;
            }
        }

        [Required]
        [AllowHtml]
        public string Content { get; set; } = "";

        public string Author { get; set; }

        public bool IsPublished { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public static BlogPost ToEntity(BlogPostModel m)
        {
            if (m == null) return null;

            return new BlogPost
            {
                Id = m.Id,
                Title = m.Title?.Trim(),
                ArticleURL = SecuritySanitizer.SanitizeHttpOrRelativeUrl(m.ArticleURL),
                MediaURL = m.SafeMediaUrl,
                Content = SecuritySanitizer.SanitizeRichHtml(m.Content),
                Author = string.IsNullOrWhiteSpace(m.Author) ? null : m.Author.Trim(),
                IsPublished = m.IsPublished,
                CreatedAt = m.CreatedAt
            };
        }

        public static BlogPostModel FromEntity(BlogPost e)
        {
            if (e == null) return null;

            return new BlogPostModel
            {
                Id = e.Id,
                Title = e.Title,
                ArticleURL = e.ArticleURL,
                MediaURL = e.MediaURL,
                Content = e.Content,
                Author = e.Author,
                IsPublished = e.IsPublished,
                CreatedAt = e.CreatedAt
            };
        }
    }
}
