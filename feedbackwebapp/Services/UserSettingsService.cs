using Microsoft.JSInterop;
using System.Text.Json;

namespace FeedbackWebApp.Services;

public class UserSettingsService : IAsyncDisposable
{
    private readonly IJSRuntime _jsRuntime;
    private const string SETTINGS_KEY = "feedbackflow_settings";
    private UserSettings? _cachedSettings;
    private bool _disposed;
    private IJSObjectReference? _jsModule;
    public class UserSettings
    {
        public int MaxCommentsToAnalyze { get; set; } = 1200;
        public bool UseCustomPrompts { get; set; } = false;
        public string? PreferredVoice { get; set; }
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

    public async Task<UserSettings> GetSettingsAsync(CancellationToken cancellationToken = default)
    {
        if (_cachedSettings != null)
            return _cachedSettings;

        var stored = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", cancellationToken, SETTINGS_KEY);
        if (string.IsNullOrEmpty(stored))
        {
            _cachedSettings = new UserSettings();
            await SaveSettingsAsync(_cachedSettings, cancellationToken);
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
                await SaveSettingsAsync(_cachedSettings, cancellationToken);
            }

            return _cachedSettings;
        }
        catch
        {
            _cachedSettings = new UserSettings();
            await SaveSettingsAsync(_cachedSettings, cancellationToken);
            return _cachedSettings;
        }
    }
    public async Task SaveSettingsAsync(UserSettings settings, CancellationToken cancellationToken = default)
    {
        var json = JsonSerializer.Serialize(settings);
        await _jsRuntime.InvokeVoidAsync("localStorage.setItem", cancellationToken, SETTINGS_KEY, json);
        _cachedSettings = settings;
    }
    
    public async Task<string?> GetPreferredVoiceAsync(CancellationToken cancellationToken = default)
    {
        var settings = await GetSettingsAsync(cancellationToken);
        return settings.PreferredVoice;
    }
    
    public async Task SetPreferredVoiceAsync(string? voiceUri, CancellationToken cancellationToken = default)
    {
        var settings = await GetSettingsAsync(cancellationToken);
        settings.PreferredVoice = voiceUri;
        await SaveSettingsAsync(settings, cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            if (_jsModule != null)
            {
                try
                {
                    await _jsModule.DisposeAsync();
                    _jsModule = null;
                }
                catch
                {
                    // Ignore errors during disposal
                }
            }
            
            _disposed = true;
        }
    }
}