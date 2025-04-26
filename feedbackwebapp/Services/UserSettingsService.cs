using Microsoft.JSInterop;
using System.Text.Json;

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
        public Dictionary<string, string> ServicePrompts { get; set; } = new()
        {
            ["youtube"] = SharedDump.AI.FeedbackAnalyzerService.GetServiceSpecificPrompt("youtube"),
            ["github"] = SharedDump.AI.FeedbackAnalyzerService.GetServiceSpecificPrompt("github"),
            ["hackernews"] = SharedDump.AI.FeedbackAnalyzerService.GetServiceSpecificPrompt("hackernews"),
            ["reddit"] = SharedDump.AI.FeedbackAnalyzerService.GetServiceSpecificPrompt("reddit"),
            ["devblogs"] = SharedDump.AI.FeedbackAnalyzerService.GetServiceSpecificPrompt("devblogs"),
            ["twitter"] = SharedDump.AI.FeedbackAnalyzerService.GetServiceSpecificPrompt("twitter")
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
}