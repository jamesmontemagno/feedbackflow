using System.Text.Json;
using SharedDump.Models;

namespace SharedDump.Utils;

public static class UsageLimitErrorHelper
{
    /// <summary>
    /// Tries to parse an error message as a usage limit exceeded JSON response
    /// </summary>
    /// <param name="errorMessage">The error message to parse</param>
    /// <param name="limitError">The parsed UsageLimitError if successful</param>
    /// <returns>True if the error is a usage limit error, false otherwise</returns>
    public static bool TryParseUsageLimitError(string errorMessage, out UsageLimitError? limitError)
    {
        limitError = null;
        
        if (string.IsNullOrWhiteSpace(errorMessage))
            return false;
            
        try
        {
            // Try to parse the error message as JSON
            if (errorMessage.Contains("USAGE_LIMIT_EXCEEDED") || errorMessage.Contains("ErrorCode"))
            {
                limitError = JsonSerializer.Deserialize<UsageLimitError>(errorMessage);
                return limitError != null && limitError.ErrorCode == "USAGE_LIMIT_EXCEEDED";
            }
        }
        catch
        {
            // Not a JSON error, continue with normal error handling
        }
        
        return false;
    }
}
