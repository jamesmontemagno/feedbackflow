using Azure.Communication.Email;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SharedDump.Models.Email;
using SharedDump.Models.Reports;
using SharedDump.Utils;

namespace FeedbackFunctions.Services.Email;

public class EmailService : IEmailService
{
    private readonly EmailClient _emailClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger _logger;
    private readonly string _senderEmail;
    private readonly string _senderName;

    public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
    {
        _configuration = configuration;
        _logger = logger;
        
        var connectionString = _configuration["AzureCommunicationServices:ConnectionString"];
        if (string.IsNullOrEmpty(connectionString))
        {
            throw new ArgumentException("AzureCommunicationServices:ConnectionString is required");
        }
        
        _emailClient = new EmailClient(connectionString);
        _senderEmail = _configuration["AzureCommunicationServices:SenderEmail"] ?? "reports@feedbackflow.dev";
        _senderName = _configuration["Email:DefaultSenderName"] ?? "FeedbackFlow Reports";
    }

    public async Task<EmailDeliveryResult> SendReportEmailAsync(ReportEmailRequest request)
    {
        try
        {
            var emailContent = new EmailContent(request.Subject)
            {
                Html = request.HtmlContent
            };

            var emailMessage = new EmailMessage(
                senderAddress: _senderEmail,
                content: emailContent,
                recipients: new EmailRecipients(new[] { new EmailAddress(request.RecipientEmail) }));

            _logger.LogInformation("Sending {ReportType} report email to {Email} with subject: {Subject}", 
                request.ReportType, request.RecipientEmail, request.Subject);

            var response = await _emailClient.SendAsync(Azure.WaitUntil.Started, emailMessage);

            return new EmailDeliveryResult
            {
                Success = true,
                MessageId = response.Id,
                SentAt = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {Email} for report {ReportId}", 
                request.RecipientEmail, request.ReportId);
            
            return new EmailDeliveryResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                SentAt = DateTime.UtcNow
            };
        }
    }

    public async Task<EmailDeliveryResult> SendReportNotificationAsync(string recipientEmail, ReportModel report)
    {
        try
        {
            var subject = GenerateEmailSubject(report);
            var htmlContent = GenerateEmailContent(report);

            var request = new ReportEmailRequest
            {
                RecipientEmail = recipientEmail,
                ReportType = report.Source,
                Subject = subject,
                HtmlContent = htmlContent,
                ReportId = report.Id.ToString()
            };

            return await SendReportEmailAsync(request);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate and send report notification for {ReportId} to {Email}", 
                report.Id, recipientEmail);
            
            return new EmailDeliveryResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                SentAt = DateTime.UtcNow
            };
        }
    }

    private string GenerateEmailSubject(ReportModel report)
    {
        return report.Source.ToLower() switch
        {
            "reddit" => $"📊 Weekly r/{report.SubSource} Report - {report.GeneratedAt:MMM d, yyyy}",
            "github" => $"🔍 GitHub Issues Report: {report.SubSource} - {report.GeneratedAt:MMM d, yyyy}",
            _ => $"📈 FeedbackFlow Report: {report.Source} - {report.GeneratedAt:MMM d, yyyy}"
        };
    }

    private string GenerateEmailContent(ReportModel report)
    {
        // Use existing email utilities if available, otherwise use HTML content
        if (!string.IsNullOrEmpty(report.HtmlContent))
        {
            return report.HtmlContent;
        }

        // Fallback to a simple HTML template
        return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>{report.Source} Report</title>
    <style>
        body {{ font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; line-height: 1.6; color: #333; max-width: 800px; margin: 0 auto; padding: 20px; }}
        .header {{ background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); color: white; padding: 20px; border-radius: 5px; margin-bottom: 20px; }}
        .content {{ background-color: #f8f9fa; padding: 20px; border-radius: 5px; margin-bottom: 20px; }}
        .footer {{ text-align: center; color: #7c7c7c; font-size: 0.9em; margin-top: 20px; }}
        .button {{ display: inline-block; background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); color: white; padding: 12px 24px; text-decoration: none; border-radius: 5px; margin: 10px 0; }}
    </style>
</head>
<body>
    <div class='header'>
        <h1>{report.Source} Report</h1>
        <p>Generated on {report.GeneratedAt:MMMM dd, yyyy} at {report.GeneratedAt:HH:mm} UTC</p>
    </div>
    
    <div class='content'>
        <p>Your FeedbackFlow report is ready! Click the button below to view the full report online.</p>
        <a href='https://www.feedbackflow.app/report/{report.Id}' class='button'>View Full Report</a>
        
        <h3>Report Summary:</h3>
        <ul>
            <li><strong>Source:</strong> {report.Source}</li>
            <li><strong>Sub-source:</strong> {report.SubSource}</li>
            <li><strong>Threads:</strong> {report.ThreadCount}</li>
            <li><strong>Comments:</strong> {report.CommentCount}</li>
            <li><strong>Period:</strong> {(report.GeneratedAt - report.CutoffDate).Days} days</li>
        </ul>
    </div>
    
    <div class='footer'>
        Generated by <a href='https://www.feedbackflow.app' style='color: #667eea;'>FeedbackFlow</a> 🥰
    </div>
</body>
</html>";
    }
}