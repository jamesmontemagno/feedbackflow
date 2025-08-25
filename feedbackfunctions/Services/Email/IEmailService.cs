using FeedbackFunctions.Models.Email;
using SharedDump.Models.Reports;

namespace FeedbackFunctions.Services.Email;

public interface IEmailService
{
    /// <summary>
    /// Sends a report notification email to the specified recipient
    /// </summary>
    Task<EmailDeliveryStatus> SendReportEmailAsync(ReportEmailRequest request);
    
    /// <summary>
    /// Sends a weekly digest email with multiple reports
    /// </summary>
    Task<EmailDeliveryStatus> SendWeeklyDigestAsync(string recipientEmail, string recipientName, List<ReportModel> reports);
    
    /// <summary>
    /// Sends a weekly admin report with dashboard statistics to administrators
    /// </summary>
    Task<EmailDeliveryStatus> SendAdminWeeklyReportAsync(AdminWeeklyReportEmailRequest request);
    
    /// <summary>
    /// Gets the delivery status of a previously sent email
    /// </summary>
    Task<EmailDeliveryStatus> GetDeliveryStatusAsync(string operationId);
    
    /// <summary>
    /// Validates if an email address is properly formatted
    /// </summary>
    bool IsValidEmailAddress(string email);
}