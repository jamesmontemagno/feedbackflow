using System;
using System.ComponentModel.DataAnnotations;
using Azure;
using Azure.Data.Tables;

namespace SharedDump.Models.Reports;

public class AdminReportConfigModel : ITableEntity
{
    public string Id { get; set; } = string.Empty;
    
    [Required(ErrorMessage = "Report name is required")]
    [StringLength(100, ErrorMessage = "Report name must be less than 100 characters")]
    public string Name { get; set; } = string.Empty; // Display name for the report
    
    [Required(ErrorMessage = "Report type is required")]
    public string Type { get; set; } = string.Empty; // "reddit" or "github"
    
    public string? Subreddit { get; set; } // if type === "reddit"
    public string? Owner { get; set; } // if type === "github"
    public string? Repo { get; set; } // if type === "github"
    
    [Required(ErrorMessage = "Email recipient is required")]
    [EmailAddress(ErrorMessage = "Please enter a valid email address")]
    public string EmailRecipient { get; set; } = string.Empty; // Email to send report to
    
    public bool IsActive { get; set; } = true; // Whether to process this report
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LastProcessedAt { get; set; } // When this report was last processed
    public string CreatedBy { get; set; } = string.Empty; // Admin user who created this

    // ITableEntity properties
    public string PartitionKey { get; set; } = "AdminReports"; // Single partition for all admin reports
    public string RowKey { get; set; } = string.Empty;
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }
}