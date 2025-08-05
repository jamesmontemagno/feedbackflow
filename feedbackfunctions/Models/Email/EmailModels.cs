using Azure.Communication.Email;
using SharedDump.Models.Reports;

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

public class EmailDeliveryStatus
{
    public string OperationId { get; set; } = string.Empty;
    public EmailSendStatus Status { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    public DateTime SentAt { get; set; }
}

public class WeeklyReportEmailModel
{
    public string RecipientEmail { get; set; } = string.Empty;
    public string RecipientName { get; set; } = string.Empty;
    public string ReportTitle { get; set; } = string.Empty;
    public ReportModel? ReportData { get; set; }
    public DateTime WeekStartDate { get; set; }
    public DateTime WeekEndDate { get; set; }
    public bool IsAdminReport { get; set; } = false;
}