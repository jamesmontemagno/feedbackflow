using System;

namespace SharedDump.Models.Reports;

public class ReportRequestModel
{
    public string Id { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty; // "reddit" or "github"
    public string? Subreddit { get; set; } // if type === "reddit"
    public string? Owner { get; set; } // if type === "github"
    public string? Repo { get; set; } // if type === "github"
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public int SubscriberCount { get; set; } = 1; // Track how many users want this report
}