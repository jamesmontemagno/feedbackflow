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
        Analysis,
        FeedQuery,
        ReportCreated,
        ReportDeleted
    }
}
