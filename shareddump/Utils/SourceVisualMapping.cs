using System;
using System.Collections.Generic;

namespace SharedDump.Utils;

/// <summary>
/// Visual styling information for a platform source
/// </summary>
/// <param name="Icon">Bootstrap icon class (e.g., "bi-youtube")</param>
/// <param name="CssClass">CSS class for styling and color</param>
public readonly record struct SourceVisual(string Icon, string CssClass);

/// <summary>
/// Provides consistent visual mapping for different platform sources
/// </summary>
public static class SourceVisualMapping
{
    /// <summary>
    /// Maps platform source types to their visual representation
    /// </summary>
    private static readonly Dictionary<string, SourceVisual> SourceMappings = new(StringComparer.OrdinalIgnoreCase)
    {
        ["YouTube"] = new("bi-youtube", "text-danger"),
        ["Reddit"] = new("bi-reddit", "text-warning"),
        ["GitHub"] = new("bi-github", "text-secondary"),
        ["GitHub Issue"] = new("bi-github", "text-secondary"),
        ["GitHub PR"] = new("bi-git", "text-primary"),
        ["GitHub Discussion"] = new("bi-chat-text", "text-info"),
        ["DevBlogs"] = new("bi-file-text", "text-success"),
        ["HackerNews"] = new("bi-hacker-news", "text-warning"),
        ["Twitter"] = new("bi-twitter-x", "text-dark"),
        ["BlueSky"] = new("bi-cloud", "text-primary"),
        ["Manual"] = new("bi-pencil-square", "text-muted"),
        ["Auto"] = new("bi-magic", "text-primary")
    };

    /// <summary>
    /// Default visual for unknown source types
    /// </summary>
    private static readonly SourceVisual DefaultVisual = new("bi-chat-dots", "text-muted");

    /// <summary>
    /// Gets the visual representation for a given source type
    /// </summary>
    /// <param name="sourceType">The source type identifier</param>
    /// <returns>Visual styling information</returns>
    public static SourceVisual GetVisual(string? sourceType)
    {
        if (string.IsNullOrWhiteSpace(sourceType))
            return DefaultVisual;

        return SourceMappings.TryGetValue(sourceType, out var visual) ? visual : DefaultVisual;
    }

    /// <summary>
    /// Gets all available source mappings
    /// </summary>
    /// <returns>Dictionary of all source type mappings</returns>
    public static IReadOnlyDictionary<string, SourceVisual> GetAllMappings()
    {
        return SourceMappings;
    }
}