namespace FeedbackWebApp.Services.ContentFeed;

public abstract class ContentFeedService
{
    protected readonly HttpClient Http;
    protected readonly IConfiguration Configuration;
    protected readonly string BaseUrl;
    private readonly Authentication.IAuthenticationHeaderService? _authHeaderService;

    protected ContentFeedService(IHttpClientFactory http, IConfiguration configuration, Authentication.IAuthenticationHeaderService? authHeaderService = null)
    {
        Http = http.CreateClient("DefaultClient");
        Configuration = configuration;
        BaseUrl = configuration["FeedbackApi:BaseUrl"] 
            ?? throw new InvalidOperationException("API base URL not configured");
        _authHeaderService = authHeaderService;
    }

    protected async Task<HttpResponseMessage> SendAuthenticatedRequestWithUsageLimitCheckAsync(string requestUri)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        if (_authHeaderService != null)
        {
            await _authHeaderService.AddAuthenticationHeadersAsync(request);
        }

        var response = await Http.SendAsync(request);

        if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            if (SharedDump.Utils.UsageLimitErrorHelper.TryParseUsageLimitError(errorContent, response.StatusCode, out var limitError) && limitError != null)
            {
                throw new SharedDump.Utils.UsageLimitExceededException(limitError);
            }
        }

        response.EnsureSuccessStatusCode();
        return response;
    }

    public abstract Task<object?> FetchContent();
}