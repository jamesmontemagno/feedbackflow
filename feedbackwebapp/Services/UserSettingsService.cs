using Microsoft.JSInterop;
using Microsoft.Extensions.Configuration;
using System.Text.Json;

namespace FeedbackWebApp.Services;

public class UserSettingsService
{
    private readonly IJSRuntime _jsRuntime;
    private readonly IConfiguration _configuration;
    private readonly bool _authDebugEnabled;
    private const string SETTINGS_KEY = "feedbackflow_settings_v2";
    private const string LAST_LOGIN_KEY = "feedbackflow_last_login";
    private const string LOGIN_ATTEMPT_KEY = "feedbackflow_loginattempt";
    private UserSettings? _cachedSettings;
    public class UserSettings
    {
        public int MaxCommentsToAnalyze { get; set; } = 1200;
        public bool UseCustomPrompts { get; set; } = false;
        public string? PreferredVoice { get; set; }
        public DateTime? LastFeatureAnnouncementShown { get; set; }
        
        // New universal prompt property
        public string UniversalPrompt { get; set; } = SharedDump.AI.FeedbackAnalyzerService.GetUniversalPrompt();
        
        // Manual prompt property - separate from universal prompt
        public string ManualPrompt { get; set; } = SharedDump.AI.FeedbackAnalyzerService.GetServiceSpecificPrompt("manual");
        
        // Feature flags
        public bool UseUnifiedThreads { get; set; } = false;
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

            // Migration: If UniversalPrompt is empty or missing, initialize it
            bool needsMigration = false;
            if (string.IsNullOrWhiteSpace(_cachedSettings.UniversalPrompt))
            {
                _cachedSettings.UniversalPrompt = SharedDump.AI.FeedbackAnalyzerService.GetUniversalPrompt();
                needsMigration = true;
            }
            
            // Migration: If ManualPrompt is empty or missing, initialize it
            if (string.IsNullOrWhiteSpace(_cachedSettings.ManualPrompt))
            {
                _cachedSettings.ManualPrompt = SharedDump.AI.FeedbackAnalyzerService.GetServiceSpecificPrompt("manual");
                needsMigration = true;
            }

            // Save if any migration was needed
            if (needsMigration)
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

    public async Task<bool> ShouldUseUnifiedThreadsAsync()
    {
        // Check configuration first (for global override)
        var configValue = _configuration.GetValue<bool?>("Features:UnifiedThreads");
        if (configValue.HasValue)
            return configValue.Value;

        // Fall back to user settings
        var settings = await GetSettingsAsync();
        return settings.UseUnifiedThreads;
    }

    public async Task SetUseUnifiedThreadsAsync(bool useUnifiedThreads)
    {
        var settings = await GetSettingsAsync();
        settings.UseUnifiedThreads = useUnifiedThreads;
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

    public async Task<bool> GetLoginAttemptAsync()
    {
        var stored = await GetStringFromLocalStorageAsync(LOGIN_ATTEMPT_KEY);
        return !string.IsNullOrEmpty(stored) && stored == "true";
    }

    public async Task ClearLoginAttemptAsync()
    {
        await RemoveFromLocalStorageAsync(LOGIN_ATTEMPT_KEY);
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