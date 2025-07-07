namespace SharedDump.Models.Account;

public class UsageValidationResult
{
    public bool IsWithinLimit { get; set; }
    public UsageType UsageType { get; set; }
    public int CurrentUsage { get; set; }
    public int Limit { get; set; }
    public AccountTier CurrentTier { get; set; }
    public string? UpgradeUrl { get; set; }
    public DateTimeOffset? ResetDate { get; set; }
    public string? ErrorMessage { get; set; }
    
    // Additional properties for usage limit errors
    public string ErrorCode { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

