using System;
using System.ComponentModel.DataAnnotations;
using System.Web.Mvc;
using GTX.Helpers;

namespace GTX.Models
{
    public class SpecialModel
    {
        public int Id { get; set; }
        [Required, StringLength(200)]
        public string Title { get; set; }
        [Required, AllowHtml]
        public string CardContent { get; set; }
        public bool IsPublished { get; set; }
        public DateTime CreatedAt { get; set; }

        public void Sanitize()
        {
            Title = Title?.Trim();
            CardContent = SecuritySanitizer.SanitizeRichHtml(CardContent);
        }
    }
}
