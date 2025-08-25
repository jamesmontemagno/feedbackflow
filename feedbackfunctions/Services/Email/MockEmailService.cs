using Azure.Communication.Email;
using FeedbackFunctions.Models.Email;
using Microsoft.Extensions.Logging;
using SharedDump.Models.Reports;
using System.Text.RegularExpressions;

namespace FeedbackFunctions.Services.Email;

/// <summary>
/// Mock implementation of email service for development and testing
/// </summary>
public class MockEmailService : IEmailService
{
    private readonly ILogger<MockEmailService> _logger;
    private static readonly Regex EmailRegex = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);

    public MockEmailService(ILogger<MockEmailService> logger)
    {
        _logger = logger;
    }

    public async Task<EmailDeliveryStatus> SendReportEmailAsync(ReportEmailRequest request)
    {
        await Task.Delay(500); // Simulate network delay

        _logger.LogInformation("Mock Email: Sending report notification to {Email}", request.RecipientEmail);
        _logger.LogInformation("Mock Email: Subject: New {ReportType} Report - {Title}", request.ReportType, request.ReportTitle);
        _logger.LogInformation("Mock Email: Report URL: {Url}", request.ReportUrl);

        return new EmailDeliveryStatus
        {
            OperationId = $"mock-{Guid.NewGuid()}",
            Status = EmailSendStatus.Succeeded,
            SentAt = DateTime.UtcNow
        };
    }

    public async Task<EmailDeliveryStatus> SendWeeklyDigestAsync(string recipientEmail, string recipientName, List<ReportModel> reports)
    {
        await Task.Delay(800); // Simulate network delay

        _logger.LogInformation("Mock Email: Sending weekly digest to {Email} ({Name})", recipientEmail, recipientName);
        _logger.LogInformation("Mock Email: Weekly digest contains {Count} reports", reports.Count);

        return new EmailDeliveryStatus
        {
            OperationId = $"mock-digest-{Guid.NewGuid()}",
            Status = EmailSendStatus.Succeeded,
            SentAt = DateTime.UtcNow
        };
    }

    public async Task<EmailDeliveryStatus> SendAdminWeeklyReportAsync(AdminWeeklyReportEmailRequest request)
    {
        await Task.Delay(600); // Simulate network delay

        _logger.LogInformation("Mock Email: Sending admin weekly report to {Email} ({Name})", request.RecipientEmail, request.RecipientName);
        _logger.LogInformation("Mock Email: Report week ending {WeekEnding}", request.ReportWeekEnding);
        _logger.LogInformation("Mock Email: Total users: {TotalUsers}, Active report configs: {ActiveConfigs}", 
            request.DashboardMetrics.UserStats.TotalUsers, request.TotalActiveReportConfigs);

        return new EmailDeliveryStatus
        {
            OperationId = $"mock-admin-weekly-{Guid.NewGuid()}",
            Status = EmailSendStatus.Succeeded,
            SentAt = DateTime.UtcNow
        };
    }

    public async Task<EmailDeliveryStatus> GetDeliveryStatusAsync(string operationId)
    {
        await Task.Delay(100); // Simulate network delay

        _logger.LogInformation("Mock Email: Checking delivery status for operation {OperationId}", operationId);

        return new EmailDeliveryStatus
        {
            OperationId = operationId,
            Status = EmailSendStatus.Succeeded,
            SentAt = DateTime.UtcNow.AddMinutes(-5)
        };
    }

    public bool IsValidEmailAddress(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return false;

        return EmailRegex.IsMatch(email);
    }
}