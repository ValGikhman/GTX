using System;
using System.Web.Mvc;
using Services;
namespace GTX.Models
{
    public enum AnnouncementType { Info = 0, Success = 1, Warning = 2, Danger = 3, Promo = 4 }
    public enum DisplayMode { Popup = 0, BannerTop = 1 }
    public enum TargetMode { AllPages = 0, LandingOnly = 1, UrlContains = 2, UrlStartsWith = 3 }

    public class AnnouncementModel
    {
        public int Id { get; set; }

        public string Title { get; set; }

        [AllowHtml]
        public string MessageHtml { get; set; }

        public AnnouncementType Type { get; set; }

        public DisplayMode DisplayMode { get; set; }

        public TargetMode? TargetMode { get; set; }

        public string TargetValue { get; set; }

        public DateTime DateStart { get; set; }

        public DateTime DateEnd { get; set; }

        public bool Active { get; set; }

        public DateTime DateCreated { get; set; }

        public static AnnouncementModel FromEntity(Announcement e)
        {
            if (e == null) return null;

            return new AnnouncementModel
            {
                Id = e.Id,
                Title = e.Title,
                MessageHtml = e.MessageHtml,

                Type = (AnnouncementType)e.Type,
                DisplayMode = (DisplayMode)e.DisplayMode,
                TargetMode = e.TargetMode.HasValue ? ((TargetMode)e.TargetMode.Value) : null,

                TargetValue = e.TargetValue,
                DateStart = e.DateStart,
                DateEnd = e.DateEnd,
                Active = e.Active,
                DateCreated = e.DateCreated
            };

        }
        public static Announcement ToEntity(AnnouncementModel model)
        {
            if (model == null) return null;

            return new Announcement
            {
                Id = model.Id,
                Title = model.Title?.Trim(),
                MessageHtml = model.MessageHtml,

                // enums -> int columns
                Type = (int)model.Type,
                DisplayMode = (int)model.DisplayMode,
                TargetMode = model.TargetMode.HasValue ? (int?)model.TargetMode.Value : null,

                TargetValue = string.IsNullOrWhiteSpace(model.TargetValue) ? null : model.TargetValue.Trim(),

                // dates
                DateStart = model.DateStart.Date,
                DateEnd = model.DateEnd.Date,

                Active = model.Active
            };
        }


    }
}
