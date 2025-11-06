using SharedDump.Models;

namespace SharedDump.Utils;

/// <summary>
/// Helper methods for working with AnalysisData
/// </summary>
public static class AnalysisDataHelper
{
    /// <summary>
    /// Maximum length for UserInput field to prevent Azure Table Storage property size limit issues
    /// </summary>
    public const int MaxUserInputLength = 500;
    
    /// <summary>
    /// Maximum length for Summary field to prevent Azure Table Storage property size limit issues
    /// </summary>
    public const int MaxSummaryLength = 500;

    /// <summary>
    /// Truncates a string to a specified maximum length, adding ellipsis if truncated.
    /// Azure Table Storage has a 64KB limit per property. By truncating to reasonable lengths,
    /// we ensure fields stay well within this limit while still providing useful context.
    /// </summary>
    /// <param name="text">The text to truncate</param>
    /// <param name="maxLength">Maximum length before truncation</param>
    /// <returns>Truncated text with "..." appended if it exceeds maxLength, otherwise the original text</returns>
    public static string? TruncateText(string? text, int maxLength)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        if (text.Length > maxLength)
            return text.Substring(0, maxLength) + "...";

        return text;
    }

    /// <summary>
    /// Truncates UserInput to a safe length for storage in Azure Table Storage.
    /// The full content is preserved in the FullAnalysis field in blob storage.
    /// </summary>
    /// <param name="userInput">The user input to truncate</param>
    /// <returns>Truncated user input if it exceeds MaxUserInputLength, otherwise the original input</returns>
    public static string? TruncateUserInput(string? userInput)
    {
        return TruncateText(userInput, MaxUserInputLength);
    }
    
    /// <summary>
    /// Truncates Summary to a safe length for storage in Azure Table Storage.
    /// The full content is preserved in the FullAnalysis field in blob storage.
    /// </summary>
    /// <param name="summary">The summary to truncate</param>
    /// <returns>Truncated summary if it exceeds MaxSummaryLength, otherwise the original summary</returns>
    public static string? TruncateSummary(string? summary)
    {
        return TruncateText(summary, MaxSummaryLength);
    }
}
