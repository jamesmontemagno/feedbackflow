using System;

namespace SharedDump.Models.Account;

public class UserAccount
{
    public string UserId { get; set; } = string.Empty;
    public AccountTier Tier { get; set; } = AccountTier.Free;
    public DateTime SubscriptionStart { get; set; }
    public DateTime? SubscriptionEnd { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime LastResetDate { get; set; }
    public int AnalysesUsed { get; set; }
    public int FeedQueriesUsed { get; set; }
    public int ActiveReports { get; set; }
    public int ApiUsed { get; set; }
    public string PreferredEmail { get; set; } = string.Empty;
    
    // Email notification settings
    public bool EmailNotificationsEnabled { get; set; } = false;
    public EmailReportFrequency EmailFrequency { get; set; } = EmailReportFrequency.None;
    public DateTime? LastEmailSent { get; set; }
}
