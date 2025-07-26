namespace SharedDump.Models.Email;

public class EmailDeliveryResult
{
    public bool Success { get; set; }
    public string? MessageId { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime SentAt { get; set; } = DateTime.UtcNow;
}

public class ReportEmailRequest
{
    public string RecipientEmail { get; set; } = string.Empty;
    public string ReportType { get; set; } = string.Empty; // "reddit", "github", etc.
    public string Subject { get; set; } = string.Empty;
    public string HtmlContent { get; set; } = string.Empty;
    public string? ReportId { get; set; }
}