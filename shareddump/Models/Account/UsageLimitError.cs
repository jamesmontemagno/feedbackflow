namespace SharedDump.Models;

public class UsageLimitError
{
    public string ErrorCode { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public int LimitType { get; set; }
    public int CurrentUsage { get; set; }
    public int Limit { get; set; }
    public DateTimeOffset? ResetDate { get; set; }
    public int CurrentTier { get; set; }
    public string? UpgradeUrl { get; set; }
}
