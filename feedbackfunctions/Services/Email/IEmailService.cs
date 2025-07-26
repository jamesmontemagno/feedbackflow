using SharedDump.Models.Email;
using SharedDump.Models.Reports;

namespace FeedbackFunctions.Services.Email;

public interface IEmailService
{
    /// <summary>
    /// Send a report notification email
    /// </summary>
    /// <param name="request">Email request with recipient and content</param>
    /// <returns>Email delivery result</returns>
    Task<EmailDeliveryResult> SendReportEmailAsync(ReportEmailRequest request);

    /// <summary>
    /// Generate and send email notification for a report
    /// </summary>
    /// <param name="recipientEmail">Recipient email address</param>
    /// <param name="report">Report to send</param>
    /// <returns>Email delivery result</returns>
    Task<EmailDeliveryResult> SendReportNotificationAsync(string recipientEmail, ReportModel report);
}