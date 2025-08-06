using System;

namespace SharedDump.Models.Account
{
    public class UsageRecord
    {
        public string UserId { get; set; } = string.Empty;
        public DateTime Date { get; set; }
        public UsageType Type { get; set; }
        public string ResourceId { get; set; } = string.Empty;
        public string Details { get; set; } = string.Empty;
    }

    public enum UsageType
    {
        // Feedback/Analysis
        Analysis = 0,
        FeedQuery = 1,
        ReportCreated = 2,
        ReportDeleted = 3,

        // Account actions
        Registration = 100,
        Deletion = 101,

        // API calls
        ApiCall = 200
    }
}
