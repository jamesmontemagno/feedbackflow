using SharedDump.Models.Account;

namespace SharedDump.Utils;

/// <summary>
/// Exception thrown when usage limits are exceeded
/// </summary>
public class UsageLimitExceededException : Exception
{
    public UsageValidationResult LimitError { get; }

    public UsageLimitExceededException(UsageValidationResult limitError) 
        : base($"Usage limit exceeded: {limitError.Message}")
    {
        LimitError = limitError;
    }

    public UsageLimitExceededException(UsageValidationResult limitError, Exception innerException) 
        : base($"Usage limit exceeded: {limitError.Message}", innerException)
    {
        LimitError = limitError;
    }
}
