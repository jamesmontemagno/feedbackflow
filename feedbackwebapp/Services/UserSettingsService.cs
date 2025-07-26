using Microsoft.JSInterop;
using System.Text.Json;
using SharedDump.Models.Email;

namespace FeedbackWebApp.Services;

public class UserSettingsService
{
    private readonly IJSRuntime _jsRuntime;
    private const string SETTINGS_KEY = "feedbackflow_settings";
    private UserSettings? _cachedSettings;
    public class UserSettings
    {
        public int MaxCommentsToAnalyze { get; set; } = 1200;
        public bool UseCustomPrompts { get; set; } = false;
        public string? PreferredVoice { get; set; }
        public DateTime? LastFeatureAnnouncementShown { get; set; }
        
        // Email notification preferences
        public string? Email { get; set; }
        public bool EmailNotificationsEnabled { get; set; } = false;
        public EmailReportFrequency EmailFrequency { get; set; } = EmailReportFrequency.Immediate;
        
        public Dictionary<string, string> ServicePrompts { get; set; } = new()
        {
            ["youtube"] = SharedDump.AI.FeedbackAnalyzerService.GetServiceSpecificPrompt("youtube"),
            ["github"] = SharedDump.AI.FeedbackAnalyzerService.GetServiceSpecificPrompt("github"),
            ["hackernews"] = SharedDump.AI.FeedbackAnalyzerService.GetServiceSpecificPrompt("hackernews"),
            ["reddit"] = SharedDump.AI.FeedbackAnalyzerService.GetServiceSpecificPrompt("reddit"),
            ["devblogs"] = SharedDump.AI.FeedbackAnalyzerService.GetServiceSpecificPrompt("devblogs"),
            ["twitter"] = SharedDump.AI.FeedbackAnalyzerService.GetServiceSpecificPrompt("twitter"),
            ["bluesky"] = SharedDump.AI.FeedbackAnalyzerService.GetServiceSpecificPrompt("bluesky"),
            ["manual"] = SharedDump.AI.FeedbackAnalyzerService.GetServiceSpecificPrompt("manual")
        };
    }

    public UserSettingsService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public async Task<UserSettings> GetSettingsAsync()
    {
        if (_cachedSettings != null)
            return _cachedSettings;

        var stored = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", SETTINGS_KEY);
        if (string.IsNullOrEmpty(stored))
        {
            _cachedSettings = new UserSettings();
            await SaveSettingsAsync(_cachedSettings);
            return _cachedSettings;
        }

        try
        {
            _cachedSettings = JsonSerializer.Deserialize<UserSettings>(stored) ?? new UserSettings();

            // Validate and reset any empty prompts to defaults
            bool hasEmptyPrompts = false;
            foreach (var serviceType in _cachedSettings.ServicePrompts.Keys.ToList())
            {
                if (string.IsNullOrWhiteSpace(_cachedSettings.ServicePrompts[serviceType]))
                {
                    _cachedSettings.ServicePrompts[serviceType] = SharedDump.AI.FeedbackAnalyzerService.GetServiceSpecificPrompt(serviceType);
                    hasEmptyPrompts = true;
                }
            }

            // Add any missing service prompts for new services
            var defaultPrompts = new[]
            {
                "youtube", "github", "hackernews", "reddit", "devblogs", 
                "twitter", "bluesky", "manual"
            };

            foreach (var serviceType in defaultPrompts)
            {
                if (!_cachedSettings.ServicePrompts.ContainsKey(serviceType))
                {
                    _cachedSettings.ServicePrompts[serviceType] = SharedDump.AI.FeedbackAnalyzerService.GetServiceSpecificPrompt(serviceType);
                    hasEmptyPrompts = true;
                }
            }

            // Save if any prompts were reset
            if (hasEmptyPrompts)
            {
                await SaveSettingsAsync(_cachedSettings);
            }

            return _cachedSettings;
        }
        catch
        {
            _cachedSettings = new UserSettings();
            await SaveSettingsAsync(_cachedSettings);
            return _cachedSettings;
        }
    }
    public async Task SaveSettingsAsync(UserSettings settings)
    {
        var json = JsonSerializer.Serialize(settings);
        await _jsRuntime.InvokeVoidAsync("localStorage.setItem", SETTINGS_KEY, json);
        _cachedSettings = settings;
    }
    
    public async Task<string?> GetPreferredVoiceAsync()
    {
        var settings = await GetSettingsAsync();
        return settings.PreferredVoice;
    }
    
    public async Task SetPreferredVoiceAsync(string? voiceUri)
    {
        var settings = await GetSettingsAsync();
        settings.PreferredVoice = voiceUri;
        await SaveSettingsAsync(settings);
    }

    public async Task<bool> ShouldShowFeatureAnnouncementAsync()
    {
        var settings = await GetSettingsAsync();
        
        // Define the latest feature announcement date (June 2025 features)
        var latestFeatureDate = new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        
        // Show if never shown before or if the latest features are newer than last shown
        return !settings.LastFeatureAnnouncementShown.HasValue ||
               settings.LastFeatureAnnouncementShown.Value < latestFeatureDate;
    }

    public async Task MarkFeatureAnnouncementShownAsync()
    {
        var settings = await GetSettingsAsync();
        settings.LastFeatureAnnouncementShown = DateTime.UtcNow;
        await SaveSettingsAsync(settings);
    }
}