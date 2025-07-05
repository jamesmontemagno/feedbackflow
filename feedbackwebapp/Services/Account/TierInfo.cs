using SharedDump.Models.Account;

namespace FeedbackWebApp.Services.Account;

/// <summary>
/// Information about an account tier including features and limits
/// </summary>
public class TierInfo
{
    public AccountTier Tier { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Price { get; set; } = "";
    public string[] Features { get; set; } = Array.Empty<string>();
    public AccountLimits Limits { get; set; } = new();
}
