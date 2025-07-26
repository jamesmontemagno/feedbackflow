using System.Net;
using FeedbackFunctions.Models.Email;
using FeedbackFunctions.Services.Email;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace FeedbackFunctions.Email;

public class SendReportEmailFunction
{
    private readonly IEmailService _emailService;
    private readonly ILogger<SendReportEmailFunction> _logger;

    public SendReportEmailFunction(IEmailService emailService, ILogger<SendReportEmailFunction> logger)
    {
        _emailService = emailService;
        _logger = logger;
    }

    [Function("SendReportEmail")]
    public async Task<HttpResponseData> SendReportEmailAsync(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "email/send-report")] HttpRequestData req)
    {
        try
        {
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var emailRequest = JsonSerializer.Deserialize<ReportEmailRequest>(requestBody);

            if (emailRequest == null)
            {
                var badRequestResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequestResponse.WriteStringAsync("Invalid request body");
                return badRequestResponse;
            }

            // Validate required fields
            if (string.IsNullOrWhiteSpace(emailRequest.RecipientEmail) || 
                string.IsNullOrWhiteSpace(emailRequest.ReportId) ||
                string.IsNullOrWhiteSpace(emailRequest.ReportTitle))
            {
                var badRequestResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequestResponse.WriteStringAsync("Missing required fields: RecipientEmail, ReportId, or ReportTitle");
                return badRequestResponse;
            }

            // Validate email format
            if (!_emailService.IsValidEmailAddress(emailRequest.RecipientEmail))
            {
                var badRequestResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequestResponse.WriteStringAsync("Invalid email address format");
                return badRequestResponse;
            }

            _logger.LogInformation("Sending report email to {Email} for report {ReportId}", 
                emailRequest.RecipientEmail, emailRequest.ReportId);

            var deliveryStatus = await _emailService.SendReportEmailAsync(emailRequest);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(deliveryStatus);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending report email");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync("Failed to send email notification");
            return errorResponse;
        }
    }
}