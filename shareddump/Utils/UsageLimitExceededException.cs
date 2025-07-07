using SharedDump.Models;

namespace SharedDump.Utils;

/// <summary>
/// Exception thrown when usage limits are exceeded
/// </summary>
public class UsageLimitExceededException : Exception
{
    public UsageLimitError LimitError { get; }

    public UsageLimitExceededException(UsageLimitError limitError) 
        : base($"Usage limit exceeded: {limitError.Message}")
    {
        LimitError = limitError;
    }

    public UsageLimitExceededException(UsageLimitError limitError, Exception innerException) 
        : base($"Usage limit exceeded: {limitError.Message}", innerException)
    {
        LimitError = limitError;
    }
}
