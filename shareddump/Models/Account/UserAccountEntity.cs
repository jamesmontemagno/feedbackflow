using System;
using Azure;
using Azure.Data.Tables;

namespace SharedDump.Models.Account;

public class UserAccountEntity : ITableEntity
{
    public string PartitionKey { get; set; } = string.Empty; // UserId
    public string RowKey { get; set; } = "account";
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    public int Tier { get; set; }
    public DateTime SubscriptionStart { get; set; }
    public DateTime? SubscriptionEnd { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime LastResetDate { get; set; }
    public int AnalysesUsed { get; set; }
    public int FeedQueriesUsed { get; set; }
    public int ActiveReports { get; set; }
    public string PreferredEmail { get; set; } = string.Empty;
    
    // Email notification settings
    public bool EmailNotificationsEnabled { get; set; } = false;
    public int EmailFrequency { get; set; } = (int)EmailReportFrequency.None;
    public DateTime? LastEmailSent { get; set; }
}

public class UsageRecordEntity : ITableEntity
{
    public string PartitionKey { get; set; } = string.Empty; // UserId
    public string RowKey { get; set; } = string.Empty; // Timestamp_UsageType_ResourceId
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    public int UsageType { get; set; }
    public string ResourceId { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty; // JSON
    public DateTime UsageTimestamp { get; set; }
}

