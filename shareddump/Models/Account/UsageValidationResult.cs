namespace SharedDump.Models.Account;

public class UsageValidationResult
{
    public bool IsWithinLimit { get; set; }
    public UsageType UsageType { get; set; }
    public int CurrentUsage { get; set; }
    public int Limit { get; set; }
    public AccountTier CurrentTier { get; set; }
    public string? UpgradeUrl { get; set; }
    public System.DateTime ResetDate { get; set; }
    public string? ErrorMessage { get; set; }
}

