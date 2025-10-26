using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace TestAutomationManager.Services
{
    /// <summary>
    /// Manages UI-only settings for tests (like IsActive toggle state)
    /// Stores settings in a local JSON file
    /// </summary>
    public class TestUISettingsService
    {
        private static TestUISettingsService _instance;
        private static readonly object _instanceLock = new();
        private static readonly object _fileLock = new();

        private readonly string _settingsFilePath;
        private Dictionary<int, TestUISettings> _settings;

        public static TestUISettingsService Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_instanceLock)
                    {
                        _instance ??= new TestUISettingsService();
                    }
                }
                return _instance;
            }
        }

        // ================================================
        // CONSTRUCTOR
        // ================================================

        private TestUISettingsService()
        {
            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string appFolder = Path.Combine(appDataPath, "TestAutomationManager");
            Directory.CreateDirectory(appFolder);
            _settingsFilePath = Path.Combine(appFolder, "test_ui_settings.json");

            _settings = new Dictionary<int, TestUISettings>();
            LoadSettings();

            System.Diagnostics.Debug.WriteLine($"✓ TestUISettingsService initialized");
        }

        // ================================================
        // PUBLIC METHODS
        // ================================================

        /// <summary>
        /// Get IsActive state for a test
        /// </summary>
        public bool GetIsActive(int testId)
            => _settings.TryGetValue(testId, out var s) ? s.IsActive : true;

        /// <summary>
        /// Set IsActive state for a test
        /// </summary>
        public async Task SetIsActiveAsync(int testId, bool isActive)
        {
            if (!_settings.ContainsKey(testId))
                _settings[testId] = new TestUISettings { TestId = testId };

            _settings[testId].IsActive = isActive;
            await SaveSettingsAsync();
        }

        /// <summary>
        /// Get Category for a test
        /// </summary>
        public string GetCategory(int testId)
            => _settings.TryGetValue(testId, out var s) ? (s.Category ?? "General") : "General";

        /// <summary>
        /// Set Category for a test
        /// </summary>
        public async Task SetCategoryAsync(int testId, string category)
        {
            if (!_settings.ContainsKey(testId))
                _settings[testId] = new TestUISettings { TestId = testId };

            _settings[testId].Category = category;
            await SaveSettingsAsync();
        }

        /// <summary>
        /// Remove settings for a test (when deleted)
        /// </summary>
        public async Task RemoveTestSettingsAsync(int testId)
        {
            if (_settings.Remove(testId))
                await SaveSettingsAsync();
        }

        // ================================================
        // PRIVATE METHODS
        // ================================================

        /// <summary>
        /// Load settings from file
        /// </summary>
        private void LoadSettings()
        {
            try
            {
                if (!File.Exists(_settingsFilePath)) return;

                lock (_fileLock)
                {
                    string json = File.ReadAllText(_settingsFilePath);
                    var settingsList = JsonSerializer.Deserialize<List<TestUISettings>>(json);
                    _settings.Clear();
                    if (settingsList != null)
                    {
                        foreach (var s in settingsList)
                            _settings[s.TestId] = s;
                    }
                    System.Diagnostics.Debug.WriteLine($"✓ Loaded UI settings for {_settings.Count} tests");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"⚠ Error loading UI settings: {ex.Message}");
                _settings = new Dictionary<int, TestUISettings>();
            }
        }

        /// <summary>
        /// Save settings to file
        /// </summary>
        private async Task SaveSettingsAsync()
        {
            try
            {
                var settingsList = new List<TestUISettings>(_settings.Values);
                var json = JsonSerializer.Serialize(settingsList, new JsonSerializerOptions { WriteIndented = true });

                lock (_fileLock)
                {
                    File.WriteAllText(_settingsFilePath, json);
                }

                await Task.CompletedTask;
                System.Diagnostics.Debug.WriteLine($"✓ Saved UI settings for {settingsList.Count} tests");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Error saving UI settings: {ex.Message}");
            }
        }
    }

/// <summary>
/// UI settings for a single test
/// </summary>
public class TestUISettings
{
    public int TestId { get; set; }
    public bool IsActive { get; set; } = true;
    public string Category { get; set; } = "General";
}
}
