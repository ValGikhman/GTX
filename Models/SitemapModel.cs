using System;
using System.Collections.Generic;

namespace GTX.Models
{
    public class SitemapModel
    {
        public string Loc { get; set; }
        public DateTime? LastMod { get; set; }
        public string ChangeFreq { get; set; }
        public decimal? Priority { get; set; }
    }

    public class SitemapGroup
    {
        public string Key { get; set; }
        public string Title { get; set; }
        public List<SitemapModel> Items { get; set; } = new List<SitemapModel>();
    }

    public class SitemapViewer
    {
        public string SourcePath { get; set; }
        public DateTime GeneratedUtc { get; set; }
        public List<SitemapGroup> Groups { get; set; } = new List<SitemapGroup>();
        public int TotalCount { get; set; }
    }
}
