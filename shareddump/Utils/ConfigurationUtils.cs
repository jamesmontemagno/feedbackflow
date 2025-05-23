using Microsoft.Extensions.Configuration;

namespace SharedDump.Utils;

/// <summary>
/// Provides utility methods for configuration validation and retrieval.
/// </summary>
/// <remarks>
/// This class centralizes configuration-related functionality to ensure consistent
/// configuration handling and error messages across services and components.
/// </remarks>
public static class ConfigurationUtils
{
    /// <summary>
    /// Gets a required configuration value and throws a standardized exception if the value is missing.
    /// </summary>
    /// <param name="configuration">The IConfiguration instance.</param>
    /// <param name="key">The configuration key to retrieve.</param>
    /// <param name="description">A user-friendly description of the configuration value.</param>
    /// <returns>The configuration value.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the configuration value is null or empty.</exception>
    /// <example>
    /// <code>
    /// var apiKey = ConfigurationUtils.GetRequiredValue(configuration, "Azure:OpenAI:ApiKey", "Azure OpenAI API key");
    /// </code>
    /// </example>
    public static string GetRequiredValue(IConfiguration configuration, string key, string description)
    {
        var value = configuration[key];
        if (string.IsNullOrEmpty(value))
        {
            throw new InvalidOperationException($"{description} not configured");
        }
        return value;
    }

    /// <summary>
    /// Gets a configuration value with a fallback default if the value is missing.
    /// </summary>
    /// <param name="configuration">The IConfiguration instance.</param>
    /// <param name="key">The configuration key to retrieve.</param>
    /// <param name="defaultValue">The default value to use if the configuration is missing.</param>
    /// <returns>The configuration value or default value if not found.</returns>
    /// <example>
    /// <code>
    /// var maxRetries = ConfigurationUtils.GetValueOrDefault(configuration, "Api:MaxRetries", "3");
    /// </code>
    /// </example>
    public static string GetValueOrDefault(IConfiguration configuration, string key, string defaultValue)
    {
        return configuration[key] ?? defaultValue;
    }

    /// <summary>
    /// Gets multiple required configuration values in one operation.
    /// </summary>
    /// <param name="configuration">The IConfiguration instance.</param>
    /// <param name="keyDescriptions">Dictionary of configuration keys and their descriptions.</param>
    /// <returns>Dictionary of configuration keys and their values.</returns>
    /// <exception cref="InvalidOperationException">Thrown when any required configuration value is missing.</exception>
    /// <example>
    /// <code>
    /// var configValues = ConfigurationUtils.GetRequiredValues(configuration, new Dictionary&lt;string, string&gt;
    /// {
    ///     { "Azure:OpenAI:Endpoint", "Azure OpenAI endpoint" },
    ///     { "Azure:OpenAI:ApiKey", "Azure OpenAI API key" },
    ///     { "Azure:OpenAI:Deployment", "Azure OpenAI deployment name" }
    /// });
    /// var endpoint = configValues["Azure:OpenAI:Endpoint"];
    /// </code>
    /// </example>
    public static Dictionary<string, string> GetRequiredValues(
        IConfiguration configuration, 
        Dictionary<string, string> keyDescriptions)
    {
        var result = new Dictionary<string, string>();
        
        foreach (var kvp in keyDescriptions)
        {
            result[kvp.Key] = GetRequiredValue(configuration, kvp.Key, kvp.Value);
        }
        
        return result;
    }
}