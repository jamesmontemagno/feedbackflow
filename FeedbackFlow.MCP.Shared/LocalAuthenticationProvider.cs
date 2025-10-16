namespace FeedbackFlow.MCP.Shared;

/// <summary>
/// Authentication provider for local development using environment variables
/// </summary>
public class LocalAuthenticationProvider : IAuthenticationProvider
{
    private const string ApiKeyEnvironmentVariable = "FEEDBACKFLOW_API_KEY";

    public string? GetAuthenticationToken()
    {
        var apiKey = Environment.GetEnvironmentVariable(ApiKeyEnvironmentVariable);
        return string.IsNullOrWhiteSpace(apiKey) ? null : apiKey;
    }

    public string GetAuthenticationErrorMessage()
    {
        return $"Error: {ApiKeyEnvironmentVariable} environment variable is required";
    }
}