using Azure;
using Azure.Communication.Email;
using FeedbackFunctions.Models.Email;
using FeedbackFunctions.Utils;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SharedDump.Models.Reports;
using SharedDump.Utils;
using System.Text.RegularExpressions;

namespace FeedbackFunctions.Services.Email;

/// <summary>
/// Email service implementation using Azure Communication Services
/// </summary>
public class EmailService : IEmailService
{
    private readonly EmailClient _emailClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<EmailService> _logger;
    private readonly string _senderEmail;
    private readonly string _senderName;
    private static readonly Regex EmailRegex = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);

    public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
    {
        _configuration = configuration;
        _logger = logger;
        
        var connectionString = configuration["AzureCommunicationServices:ConnectionString"];
        if (string.IsNullOrEmpty(connectionString))
            throw new InvalidOperationException("Azure Communication Services connection string not configured");
            
        _emailClient = new EmailClient(connectionString);
        _senderEmail = configuration["AzureCommunicationServices:SenderEmail"] ?? "donotreply@feedbackflow.app";
        _senderName = configuration["Email:DefaultSenderName"] ?? "FeedbackFlow Reports";
    }

    public async Task<EmailDeliveryStatus> SendReportEmailAsync(ReportEmailRequest request)
    {
        try
        {
            if (!IsValidEmailAddress(request.RecipientEmail))
            {
                return new EmailDeliveryStatus
                {
                    OperationId = string.Empty,
                    Status = EmailSendStatus.Failed,
                    ErrorMessage = "Invalid recipient email address",
                    SentAt = DateTime.UtcNow
                };
            }

            var emailTemplate = GenerateReportEmailTemplate(request);
            
            var emailMessage = new EmailMessage(
                senderAddress: _senderEmail,
                recipientAddress: request.RecipientEmail,
                content: new EmailContent(emailTemplate.Subject)
                {
                    Html = emailTemplate.HtmlContent,
                    PlainText = emailTemplate.PlainTextContent
                }
            );

            _logger.LogInformation("Sending report email to {Email} for report {ReportId}", 
                request.RecipientEmail, request.ReportId);

            var response = await _emailClient.SendAsync(Azure.WaitUntil.Started, emailMessage);
            
            return new EmailDeliveryStatus
            {
                OperationId = response.Id,
                Status = EmailSendStatus.Running,
                SentAt = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send report email to {Email}", request.RecipientEmail);
            
            return new EmailDeliveryStatus
            {
                OperationId = string.Empty,
                Status = EmailSendStatus.Failed,
                ErrorMessage = ex.Message,
                SentAt = DateTime.UtcNow
            };
        }
    }

    public async Task<EmailDeliveryStatus> SendWeeklyDigestAsync(string recipientEmail, string recipientName, List<ReportModel> reports)
    {
        try
        {
            if (!IsValidEmailAddress(recipientEmail))
            {
                return new EmailDeliveryStatus
                {
                    OperationId = string.Empty,
                    Status = EmailSendStatus.Failed,
                    ErrorMessage = "Invalid recipient email address",
                    SentAt = DateTime.UtcNow
                };
            }

            var emailTemplate = GenerateWeeklyDigestTemplate(recipientName, reports);
            
            var emailMessage = new EmailMessage(
                senderAddress: _senderEmail,
                recipientAddress: recipientEmail,
                content: new EmailContent(emailTemplate.Subject)
                {
                    Html = emailTemplate.HtmlContent,
                    PlainText = emailTemplate.PlainTextContent
                }
            );

            _logger.LogInformation("Sending weekly digest email to {Email} with {Count} reports", 
                recipientEmail, reports.Count);

            var response = await _emailClient.SendAsync(Azure.WaitUntil.Started, emailMessage);
            
            return new EmailDeliveryStatus
            {
                OperationId = response.Id,
                Status = EmailSendStatus.Running,
                SentAt = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send weekly digest email to {Email}", recipientEmail);
            
            return new EmailDeliveryStatus
            {
                OperationId = string.Empty,
                Status = EmailSendStatus.Failed,
                ErrorMessage = ex.Message,
                SentAt = DateTime.UtcNow
            };
        }
    }

    public async Task<EmailDeliveryStatus> GetDeliveryStatusAsync(string operationId)
    {
        try
        {
            // Note: GetSendResultAsync may not be available in all versions
            // For now, we'll return a placeholder status
            await Task.Delay(100); // Simulate async operation
            
            _logger.LogInformation("Checking delivery status for operation {OperationId}", operationId);
            
            return new EmailDeliveryStatus
            {
                OperationId = operationId,
                Status = EmailSendStatus.Running, // Default to running status
                ErrorMessage = string.Empty,
                SentAt = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get delivery status for operation {OperationId}", operationId);
            
            return new EmailDeliveryStatus
            {
                OperationId = operationId,
                Status = EmailSendStatus.Failed,
                ErrorMessage = ex.Message,
                SentAt = DateTime.UtcNow
            };
        }
    }

    public bool IsValidEmailAddress(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return false;

        return EmailRegex.IsMatch(email);
    }

    private EmailTemplate GenerateReportEmailTemplate(ReportEmailRequest request)
    {
        var subject = $"📊 Your {request.ReportType} Report is Ready - {request.ReportTitle}";

        var htmlContent = request.HtmlContent;
        
        // Use existing email template utilities, but create a simple notification template
        //         var htmlContent = $@"
        // <!DOCTYPE html>
        // <html>
        // <head>
        //     <meta charset='utf-8'>
        //     <meta name='viewport' content='width=device-width, initial-scale=1.0'>
        //     <title>{subject}</title>
        //     <style>
        //         body {{
        //             font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
        //             line-height: 1.6;
        //             color: #333;
        //             max-width: 600px;
        //             margin: 0 auto;
        //             padding: 20px;
        //             background-color: #f8f9fa;
        //         }}
        //         .header {{
        //             background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
        //             color: white;
        //             padding: 30px 20px;
        //             text-align: center;
        //             border-radius: 10px 10px 0 0;
        //         }}
        //         .content {{
        //             background: white;
        //             padding: 30px;
        //             border-radius: 0 0 10px 10px;
        //             box-shadow: 0 4px 12px rgba(0,0,0,0.1);
        //         }}
        //         .report-summary {{
        //             background: #f8f9fa;
        //             padding: 20px;
        //             border-radius: 8px;
        //             margin: 20px 0;
        //         }}
        //         .button {{
        //             display: inline-block;
        //             background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
        //             color: white;
        //             padding: 15px 30px;
        //             text-decoration: none;
        //             border-radius: 8px;
        //             font-weight: 600;
        //             margin: 20px 0;
        //         }}
        //         .footer {{
        //             text-align: center;
        //             color: #666;
        //             font-size: 0.9em;
        //             margin-top: 30px;
        //         }}
        //     </style>
        // </head>
        // <body>
        //     <div class='header'>
        //         <h1>🚀 FeedbackFlow Report Ready!</h1>
        //     </div>
        //     <div class='content'>
        //         <p>Hi {request.RecipientName}!</p>

        //         <p>Your <strong>{request.ReportType}</strong> report has been generated and is ready to view.</p>

        //         <div class='report-summary'>
        //             <h3>{request.ReportTitle}</h3>
        //             <p><strong>Generated:</strong> {request.GeneratedAt:MMM dd, yyyy 'at' h:mm tt}</p>
        //             <p><strong>Summary:</strong> {request.ReportSummary}</p>
        //         </div>

        //         <a href='{request.ReportUrl}' class='button'>📊 View Full Report</a>

        //         <p>This report contains detailed analysis and insights based on the latest feedback data. Click the button above to access the complete report with all visualizations and recommendations.</p>
        //     </div>
        //     <div class='footer'>
        //         <p>Generated by <strong>FeedbackFlow</strong> 🥰</p>
        //         <p><small>This email was sent because you requested a report notification. You can manage your email preferences in your account settings.</small></p>
        //     </div>
        // </body>
        // </html>";

        var plainTextContent = $@"
FeedbackFlow Report Ready!

Hi {request.RecipientName}!

Your {request.ReportType} report has been generated and is ready to view.

Report: {request.ReportTitle}
Generated: {request.GeneratedAt:MMM dd, yyyy 'at' h:mm tt}
Summary: {request.ReportSummary}

View your report: {request.ReportUrl}

This report contains detailed analysis and insights based on the latest feedback data.

---
Generated by FeedbackFlow
This email was sent because you requested a report notification. You can manage your email preferences in your account settings.
";

        return new EmailTemplate
        {
            Subject = subject,
            HtmlContent = htmlContent,
            PlainTextContent = plainTextContent,
            TemplateType = EmailTemplateType.ReportNotification
        };
    }

    private EmailTemplate GenerateWeeklyDigestTemplate(string recipientName, List<ReportModel> reports)
    {
        var subject = $"📈 Your Weekly FeedbackFlow Digest - {reports.Count} New Reports";
        
        var reportsList = string.Join("", reports.Select(r => $@"
            <div style='border-left: 4px solid #667eea; padding-left: 15px; margin: 15px 0;'>
                <strong>{r.Source} Report</strong><br>
                <small style='color: #666;'>Generated: {r.GeneratedAt:MMM dd, yyyy}</small><br>
                <a href='{WebUrlHelper.BuildReportQueryUrl(_configuration, r.Id, "email")}' style='color: #667eea; text-decoration: none;'>View Report →</a>
            </div>"));

        var htmlContent = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>{subject}</title>
    <style>
        body {{
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
            line-height: 1.6;
            color: #333;
            max-width: 600px;
            margin: 0 auto;
            padding: 20px;
            background-color: #f8f9fa;
        }}
        .header {{
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            color: white;
            padding: 30px 20px;
            text-align: center;
            border-radius: 10px 10px 0 0;
        }}
        .content {{
            background: white;
            padding: 30px;
            border-radius: 0 0 10px 10px;
            box-shadow: 0 4px 12px rgba(0,0,0,0.1);
        }}
        .footer {{
            text-align: center;
            color: #666;
            font-size: 0.9em;
            margin-top: 30px;
        }}
    </style>
</head>
<body>
    <div class='header'>
        <h1>📈 Weekly Digest</h1>
    </div>
    <div class='content'>
        <p>Hi {recipientName}!</p>
        
        <p>Here's your weekly summary of FeedbackFlow reports generated this week:</p>
        
        {reportsList}
        
        <p style='margin-top: 30px;'>
            <a href='{WebUrlHelper.BuildUrl(_configuration, "/reports")}' style='display: inline-block; background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); color: white; padding: 15px 30px; text-decoration: none; border-radius: 8px; font-weight: 600;'>View All Reports</a>
        </p>
    </div>
    <div class='footer'>
        <p>Generated by <strong>FeedbackFlow</strong> 🥰</p>
        <p><small>You can manage your email preferences in your account settings.</small></p>
    </div>
</body>
</html>";

        var plainTextContent = $@"
Weekly FeedbackFlow Digest

Hi {recipientName}!

Here's your weekly summary of FeedbackFlow reports generated this week:

{string.Join("\n", reports.Select(r => $"• {r.Source} Report (Generated: {r.GeneratedAt:MMM dd, yyyy}) - {WebUrlHelper.BuildReportQueryUrl(_configuration, r.Id, "email")}"))}

View all reports: {WebUrlHelper.BuildUrl(_configuration, "/reports")}

---
Generated by FeedbackFlow
You can manage your email preferences in your account settings.
";

        return new EmailTemplate
        {
            Subject = subject,
            HtmlContent = htmlContent,
            PlainTextContent = plainTextContent,
            TemplateType = EmailTemplateType.WeeklyDigest
        };
    }
}