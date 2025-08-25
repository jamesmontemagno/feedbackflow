using Azure.Communication.Email;
using SharedDump.Models.Reports;
using SharedDump.Models.Admin;

namespace FeedbackFunctions.Models.Email;

public class EmailTemplate
{
    public string Subject { get; set; } = string.Empty;
    public string HtmlContent { get; set; } = string.Empty;
    public string PlainTextContent { get; set; } = string.Empty;
    public EmailTemplateType TemplateType { get; set; }
}

public enum EmailTemplateType
{
    ReportNotification,
    WeeklyDigest,
    AdminWeeklyDigest,
    WelcomeEmail
}

public class ReportEmailRequest
{
    public string RecipientEmail { get; set; } = string.Empty;
    public string RecipientName { get; set; } = string.Empty;
    public string ReportId { get; set; } = string.Empty;
    public string ReportTitle { get; set; } = string.Empty;
    public string ReportSummary { get; set; } = string.Empty;
    public string ReportUrl { get; set; } = string.Empty;
    public string ReportType { get; set; } = string.Empty;
    public string HtmlContent { get; set; } = string.Empty;
    public DateTime GeneratedAt { get; set; }
}

public class AdminWeeklyReportEmailRequest
{
    public string RecipientEmail { get; set; } = string.Empty;
    public string RecipientName { get; set; } = string.Empty;
    public AdminDashboardMetrics DashboardMetrics { get; set; } = new();
    public DateTime ReportWeekEnding { get; set; }
    public int TotalActiveReportConfigs { get; set; }
    public int ReportsGeneratedThisWeek { get; set; }
    public List<string> TopActiveRepositories { get; set; } = new();
    public List<string> TopActiveSubreddits { get; set; } = new();
}

public class EmailDeliveryStatus
{
    public string OperationId { get; set; } = string.Empty;
    public EmailSendStatus Status { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    public DateTime SentAt { get; set; }
}
