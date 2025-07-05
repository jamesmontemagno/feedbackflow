using System;

namespace SharedDump.Models.Account
{
    public class UserAccount
    {
        public string UserId { get; set; } = string.Empty;
        public AccountTier Tier { get; set; } = AccountTier.Free;
        public DateTime SubscriptionStart { get; set; }
        public DateTime? SubscriptionEnd { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; }
        public DateTime LastResetDate { get; set; }
        public int AnalysesUsed { get; set; }
        public int FeedQueriesUsed { get; set; }
        public int ActiveReports { get; set; }
        public int AnalysisLimit { get; set; }
        public int ReportLimit { get; set; }
        public int FeedQueryLimit { get; set; }
    }

    public enum AccountTier
    {
        Free = 0,
        Pro = 1,
        ProPlus = 2
    }
}
