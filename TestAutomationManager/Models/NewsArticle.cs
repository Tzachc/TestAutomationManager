using System;

namespace TestAutomationManager.Models
{
    public class NewsArticle
    {
        public string Title { get; set; }
        public string Description { get; set; }
        public string Source { get; set; }
        public string Author { get; set; }
        public DateTime PublishedAt { get; set; }
        public string Url { get; set; }
        public string ImageUrl { get; set; }
        public string Category { get; set; }

        public string TimeAgo
        {
            get
            {
                var timeSpan = DateTime.Now - PublishedAt;
                if (timeSpan.TotalMinutes < 1) return "Just now";
                if (timeSpan.TotalMinutes < 60) return $"{(int)timeSpan.TotalMinutes}m ago";
                if (timeSpan.TotalHours < 24) return $"{(int)timeSpan.TotalHours}h ago";
                if (timeSpan.TotalDays < 7) return $"{(int)timeSpan.TotalDays}d ago";
                return PublishedAt.ToString("MMM dd, yyyy");
            }
        }
    }
}
