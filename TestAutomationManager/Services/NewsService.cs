using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using TestAutomationManager.Models;

namespace TestAutomationManager.Services
{
    public class NewsService
    {
        private static readonly HttpClient _httpClient;

        // Free API key for NewsAPI.org - Users should replace with their own
        // Get your free key at: https://newsapi.org/register
        private const string API_KEY = "YOUR_API_KEY_HERE";
        private const string BASE_URL = "https://newsapi.org/v2/everything";

        static NewsService()
        {
            _httpClient = new HttpClient();
            // NewsAPI requires a User-Agent header
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "TestAutomationManager/1.0 (Tech News Reader)");
        }

        public async Task<List<NewsArticle>> GetTechNewsAsync()
        {
            try
            {
                // If user hasn't set API key, return sample data
                if (API_KEY == "YOUR_API_KEY_HERE")
                {
                    return GetSampleNews();
                }

                var articles = new List<NewsArticle>();

                // Simplified queries - NewsAPI free tier has limitations on complex OR queries
                // Using separate simpler queries instead
                var queries = new[]
                {
                    ("selenium", "Test Automation"),
                    ("playwright", "Test Automation"),
                    ("test automation", "Test Automation"),
                    ("artificial intelligence", "AI & ML"),
                    ("machine learning", "AI & ML"),
                    ("kubernetes", "DevOps"),
                    ("docker", "DevOps"),
                    ("devops", "DevOps")
                };

                foreach (var (keyword, category) in queries)
                {
                    var categoryArticles = await FetchArticlesByKeyword(keyword, category);
                    articles.AddRange(categoryArticles);
                }

                // Remove duplicates and sort by date
                return articles
                    .GroupBy(a => a.Title)
                    .Select(g => g.First())
                    .OrderByDescending(a => a.PublishedAt)
                    .Take(30)
                    .ToList();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error fetching news: {ex.Message}");
                return GetSampleNews();
            }
        }

        private async Task<List<NewsArticle>> FetchArticlesByKeyword(string keyword, string category)
        {
            var articles = new List<NewsArticle>();

            try
            {
                // Get articles from the last 30 days (more results)
                var fromDate = DateTime.Now.AddDays(-30).ToString("yyyy-MM-dd");
                var url = $"{BASE_URL}?q={Uri.EscapeDataString(keyword)}&from={fromDate}&sortBy=publishedAt&language=en&pageSize=10&apiKey={API_KEY}";

                var response = await _httpClient.GetAsync(url);

                // Better error handling
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    System.Diagnostics.Debug.WriteLine($"API Error for '{keyword}': {response.StatusCode} - {errorContent}");
                    return articles;
                }

                var content = await response.Content.ReadAsStringAsync();
                var jsonDoc = JsonDocument.Parse(content);

                // Check for API errors in response
                if (jsonDoc.RootElement.TryGetProperty("status", out var status) && status.GetString() == "error")
                {
                    if (jsonDoc.RootElement.TryGetProperty("message", out var message))
                    {
                        System.Diagnostics.Debug.WriteLine($"NewsAPI Error: {message.GetString()}");
                    }
                    return articles;
                }

                if (jsonDoc.RootElement.TryGetProperty("articles", out var articlesArray))
                {
                    foreach (var article in articlesArray.EnumerateArray())
                    {
                        var newsArticle = new NewsArticle
                        {
                            Title = GetJsonString(article, "title"),
                            Description = GetJsonString(article, "description"),
                            Author = GetJsonString(article, "author"),
                            Url = GetJsonString(article, "url"),
                            ImageUrl = GetJsonString(article, "urlToImage"),
                            PublishedAt = GetJsonDateTime(article, "publishedAt"),
                            Source = article.TryGetProperty("source", out var source)
                                ? GetJsonString(source, "name")
                                : "Unknown",
                            Category = category
                        };

                        articles.Add(newsArticle);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error fetching articles for keyword '{keyword}': {ex.Message}");
            }

            return articles;
        }

        private string GetJsonString(JsonElement element, string propertyName)
        {
            if (element.TryGetProperty(propertyName, out var property) &&
                property.ValueKind == JsonValueKind.String)
            {
                return property.GetString() ?? string.Empty;
            }
            return string.Empty;
        }

        private DateTime GetJsonDateTime(JsonElement element, string propertyName)
        {
            if (element.TryGetProperty(propertyName, out var property) &&
                property.ValueKind == JsonValueKind.String)
            {
                if (DateTime.TryParse(property.GetString(), out var date))
                    return date;
            }
            return DateTime.Now;
        }

        // Sample news for when API key is not configured
        private List<NewsArticle> GetSampleNews()
        {
            return new List<NewsArticle>
            {
                new NewsArticle
                {
                    Title = "Getting Started with News API",
                    Description = "To see live tech news, please register for a free API key at newsapi.org and update the API_KEY in NewsService.cs",
                    Source = "Setup Guide",
                    Author = "TestAutomationManager",
                    PublishedAt = DateTime.Now,
                    Category = "Setup",
                    ImageUrl = null,
                    Url = "https://newsapi.org/register"
                },
                new NewsArticle
                {
                    Title = "Playwright vs Selenium: Modern Web Testing in 2024",
                    Description = "A comprehensive comparison of Playwright and Selenium for automated testing, exploring performance, features, and use cases.",
                    Source = "Tech News Daily",
                    Author = "Sample Author",
                    PublishedAt = DateTime.Now.AddHours(-2),
                    Category = "Test Automation",
                    ImageUrl = null,
                    Url = "https://newsapi.org/register"
                },
                new NewsArticle
                {
                    Title = "AI-Powered Test Generation: The Future of QA",
                    Description = "How artificial intelligence is revolutionizing automated test generation and improving software quality.",
                    Source = "DevOps Weekly",
                    Author = "Sample Author",
                    PublishedAt = DateTime.Now.AddHours(-5),
                    Category = "AI & ML",
                    ImageUrl = null,
                    Url = "https://newsapi.org/register"
                },
                new NewsArticle
                {
                    Title = "Kubernetes 1.29: What's New for DevOps Teams",
                    Description = "Explore the latest features and improvements in Kubernetes that are transforming container orchestration.",
                    Source = "Cloud Native News",
                    Author = "Sample Author",
                    PublishedAt = DateTime.Now.AddHours(-8),
                    Category = "DevOps",
                    ImageUrl = null,
                    Url = "https://newsapi.org/register"
                },
                new NewsArticle
                {
                    Title = "Cypress 13.0 Released with Component Testing Enhancements",
                    Description = "The latest version of Cypress brings improved component testing capabilities and better developer experience.",
                    Source = "Testing Tribune",
                    Author = "Sample Author",
                    PublishedAt = DateTime.Now.AddHours(-12),
                    Category = "Test Automation",
                    ImageUrl = null,
                    Url = "https://newsapi.org/register"
                }
            };
        }
    }
}
