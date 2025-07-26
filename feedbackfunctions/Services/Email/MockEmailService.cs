using Microsoft.Extensions.Logging;
using SharedDump.Models.Email;
using SharedDump.Models.Reports;

namespace FeedbackFunctions.Services.Email;

public class MockEmailService : IEmailService
{
    private readonly ILogger _logger;

    public MockEmailService(ILogger<MockEmailService> logger)
    {
        _logger = logger;
    }

    public Task<EmailDeliveryResult> SendReportEmailAsync(ReportEmailRequest request)
    {
        _logger.LogInformation("MOCK EMAIL: Would send {ReportType} report to {Email} with subject: {Subject}", 
            request.ReportType, request.RecipientEmail, request.Subject);
        
        return Task.FromResult(new EmailDeliveryResult
        {
            Success = true,
            MessageId = $"mock_{Guid.NewGuid():N}",
            SentAt = DateTime.UtcNow
        });
    }

    public Task<EmailDeliveryResult> SendReportNotificationAsync(string recipientEmail, ReportModel report)
    {
        var subject = $"FeedbackFlow Report: {report.Source}";
        
        _logger.LogInformation("MOCK EMAIL: Would send report {ReportId} ({ReportType}) to {Email} with subject: {Subject}", 
            report.Id, report.Source, recipientEmail, subject);
        
        return Task.FromResult(new EmailDeliveryResult
        {
            Success = true,
            MessageId = $"mock_{Guid.NewGuid():N}",
            SentAt = DateTime.UtcNow
        });
    }
}