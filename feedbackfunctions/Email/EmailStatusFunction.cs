using System.Net;
using FeedbackFunctions.Services.Email;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace FeedbackFunctions.Email;

public class EmailStatusFunction
{
    private readonly IEmailService _emailService;
    private readonly ILogger<EmailStatusFunction> _logger;

    public EmailStatusFunction(IEmailService emailService, ILogger<EmailStatusFunction> logger)
    {
        _emailService = emailService;
        _logger = logger;
    }

    [Function("GetEmailStatus")]
    public async Task<HttpResponseData> GetEmailStatusAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "email/status/{operationId}")] HttpRequestData req,
        string operationId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(operationId))
            {
                var badRequestResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequestResponse.WriteStringAsync("Operation ID is required");
                return badRequestResponse;
            }

            _logger.LogInformation("Checking email delivery status for operation {OperationId}", operationId);

            var deliveryStatus = await _emailService.GetDeliveryStatusAsync(operationId);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(deliveryStatus);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving email status for operation {OperationId}", operationId);
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync("Failed to retrieve email status");
            return errorResponse;
        }
    }
}