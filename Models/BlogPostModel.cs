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

        [Required]
        [AllowHtml]
        public string Content { get; set; } = "";

        public string? Author { get; set; }

        public bool IsPublished { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public static BlogPost ToEntity(BlogPostModel m)
        {
            if (m == null) return null;

            return new BlogPost
            {
                Id = m.Id,
                Title = m.Title,
                ArticleURL = m.ArticleURL,
                MediaURL = m.MediaURL,
                Content = m.Content,
                Author = m.Author,
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