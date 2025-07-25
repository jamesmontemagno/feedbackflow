using Microsoft.JSInterop;
using Microsoft.Extensions.Configuration;
using System.Text.Json;

namespace FeedbackWebApp.Services;

public class UserSettingsService
{
    private readonly IJSRuntime _jsRuntime;
    private readonly IConfiguration _configuration;
    private readonly bool _authDebugEnabled;
    private const string SETTINGS_KEY = "feedbackflow_settings";
    private const string LAST_LOGIN_KEY = "feedbackflow_last_login";
    private UserSettings? _cachedSettings;
    public class UserSettings
    {
        public int MaxCommentsToAnalyze { get; set; } = 1200;
        public bool UseCustomPrompts { get; set; } = false;
        public string? PreferredVoice { get; set; }
        public DateTime? LastFeatureAnnouncementShown { get; set; }
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

    public UserSettingsService(IJSRuntime jsRuntime, IConfiguration configuration)
    {
        _jsRuntime = jsRuntime;
        _configuration = configuration;
        _authDebugEnabled = _configuration.GetValue<bool>("Authentication:DEBUG", false);
    }

    public async Task<UserSettings> GetSettingsAsync()
    {
        if (_cachedSettings != null)
            return _cachedSettings;

        var stored = await GetStringFromLocalStorageAsync(SETTINGS_KEY);
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
        await SaveToLocalStorageAsync(SETTINGS_KEY, settings);
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

    public async Task<DateTime?> GetLastLoginAtAsync()
    {
        var stored = await GetStringFromLocalStorageAsync(LAST_LOGIN_KEY);
        if (string.IsNullOrEmpty(stored))
            return null;

        try
        {
            return JsonSerializer.Deserialize<DateTime?>(stored);
        }
        catch
        {
            return null;
        }
    }

    public async Task UpdateLastLoginAtAsync()
    {
        var now = DateTime.UtcNow;
        await SaveToLocalStorageAsync(LAST_LOGIN_KEY, now);
    }

    // Generic localStorage helpers
    public async Task<T?> GetFromLocalStorageAsync<T>(string key) where T : class
    {
        var stored = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", key);
        if (string.IsNullOrEmpty(stored))
            return null;

        try
        {
            return JsonSerializer.Deserialize<T>(stored);
        }
        catch
        {
            return null;
        }
    }

    public async Task<T> GetFromLocalStorageAsync<T>(string key, T defaultValue) where T : class
    {
        var result = await GetFromLocalStorageAsync<T>(key);
        return result ?? defaultValue;
    }

    public async Task SaveToLocalStorageAsync<T>(string key, T value)
    {
        var json = JsonSerializer.Serialize(value);
        await _jsRuntime.InvokeVoidAsync("localStorage.setItem", key, json);
    }

    public async Task RemoveFromLocalStorageAsync(string key)
    {
        await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", key);
    }

    public async Task<string?> GetStringFromLocalStorageAsync(string key)
    {
        return await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", key);
    }

    public async Task SaveStringToLocalStorageAsync(string key, string value)
    {
        await _jsRuntime.InvokeVoidAsync("localStorage.setItem", key, value);
    }

    // Auth debugging helpers
    public async Task LogAuthDebugAsync(string message, object? data = null)
    {
        if (_authDebugEnabled)
        {
            if (data != null)
            {
                await _jsRuntime.InvokeVoidAsync("console.log", $"[AUTH DEBUG] {message}", data);
            }
            else
            {
                await _jsRuntime.InvokeVoidAsync("console.log", $"[AUTH DEBUG] {message}");
            }
        }
    }

    public async Task LogAuthErrorAsync(string message, object? error = null)
    {
        if (_authDebugEnabled)
        {
            if (error != null)
            {
                await _jsRuntime.InvokeVoidAsync("console.error", $"[AUTH ERROR] {message}", error);
            }
            else
            {
                await _jsRuntime.InvokeVoidAsync("console.error", $"[AUTH ERROR] {message}");
            }
        }
    }

    public async Task LogAuthWarnAsync(string message, object? data = null)
    {
        if (_authDebugEnabled)
        {
            if (data != null)
            {
                await _jsRuntime.InvokeVoidAsync("console.warn", $"[AUTH WARN] {message}", data);
            }
            else
            {
                await _jsRuntime.InvokeVoidAsync("console.warn", $"[AUTH WARN] {message}");
            }
        }
    }
}