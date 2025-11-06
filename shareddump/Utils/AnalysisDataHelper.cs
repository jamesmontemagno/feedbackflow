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
    /// Truncates UserInput to a safe length for storage in Azure Table Storage.
    /// Azure Table Storage has a 64KB limit per property. By truncating to 500 characters,
    /// we ensure the UserInput field stays well within this limit while still providing
    /// useful context. The full content is preserved in the FullAnalysis field in blob storage.
    /// </summary>
    /// <param name="userInput">The user input to truncate</param>
    /// <returns>Truncated user input if it exceeds MaxUserInputLength, otherwise the original input</returns>
    public static string? TruncateUserInput(string? userInput)
    {
        if (string.IsNullOrEmpty(userInput))
            return userInput;

        if (userInput.Length > MaxUserInputLength)
            return userInput.Substring(0, MaxUserInputLength) + "...";

        return userInput;
    }
}
