using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SharedDump.Models.Reddit;
using SharedDump.AI;
using SharedDump.Utils;

namespace FeedbackFunctions;

public class ReportingFunctions
{
    private readonly ILogger<ReportingFunctions> _logger;
    private readonly RedditService _redditService;
    private readonly IFeedbackAnalyzerService _analyzerService;
    private readonly IConfiguration _configuration;

    public ReportingFunctions(
        ILogger<ReportingFunctions> logger,
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory)
    {

#if DEBUG

        _configuration = new ConfigurationBuilder()
                    .AddJsonFile("local.settings.json")
                    .AddUserSecrets<Program>()
                    .Build();
#else

        _configuration = configuration;
#endif
        _logger = logger;

        var redditClientId = _configuration["Reddit:ClientId"] ?? throw new InvalidOperationException("Reddit client ID not configured");
        var redditClientSecret = _configuration["Reddit:ClientSecret"] ?? throw new InvalidOperationException("Reddit client secret not configured");
        _redditService = new RedditService(redditClientId, redditClientSecret, httpClientFactory.CreateClient("Reddit"));

        var endpoint = _configuration["Azure:OpenAI:Endpoint"] ?? throw new InvalidOperationException("Azure OpenAI endpoint not configured");
        var apiKey = _configuration["Azure:OpenAI:ApiKey"] ?? throw new InvalidOperationException("Azure OpenAI API key not configured");
        var deployment = _configuration["Azure:OpenAI:Deployment"] ?? throw new InvalidOperationException("Azure OpenAI deployment name not configured");
        _analyzerService = new FeedbackAnalyzerService(endpoint, apiKey, deployment);
    }

    [Function("RedditReport")]
    public async Task<HttpResponseData> RedditReport(
        [HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequestData req)
    {
        _logger.LogInformation("Processing Reddit Report request");

        var queryParams = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
        var subreddit = queryParams["subreddit"];

        if (string.IsNullOrEmpty(subreddit))
        {
            var response = req.CreateResponse(HttpStatusCode.BadRequest);
            await response.WriteStringAsync("Subreddit parameter is required");
            return response;
        }

        try
        {
            // Get threads from the last 7 days
            var cutoffDate = DateTimeOffset.UtcNow.AddDays(-7);
            var threads = await _redditService.GetSubredditThreadsBasicInfo(subreddit, "hot", cutoffDate);

            // Sort threads by engagement (combination of comments and score)
            var topThreads = threads
                .OrderByDescending(t => (t.NumComments * 0.7) + (t.Score * 0.3))
                .Take(5)
                .ToList();

            // Get full thread details with comments
            var threadAnalyses = new List<(RedditThreadModel Thread, string Analysis)>();
            var allComments = new List<RedditCommentModel>();

            foreach (var thread in topThreads)
            {
                var fullThread = await _redditService.GetThreadWithComments(thread.Id);
                var threadContent = $"Title: {fullThread.Title}\n\nContent: {fullThread.SelfText}\n\nComments:\n";
                
                // Collect all comments for the thread
                var flatComments = FlattenComments(fullThread.Comments);
                allComments.AddRange(flatComments);
                
                threadContent += string.Join("\n", flatComments.Select(c => $"Comment by {c.Author}: {c.Body}"));

                // Analyze each thread individually
                var threadAnalysis = await _analyzerService.AnalyzeCommentsAsync("reddit", threadContent);
                threadAnalyses.Add((fullThread, threadAnalysis));
            }

            // Get top comments from the week
            var topComments = allComments
                .OrderByDescending(c => c.Score)
                .Take(5)
                .ToList();

            // Create a weekly summary
            var weeklyContent = $@"Analyze the following week of Reddit discussions from r/{subreddit}. Focus on:
1. Overall community sentiment and trends
2. Key themes and topics that emerged
3. Notable insights or recurring pain points
4. The quality and nature of community engagement
5. Any actionable recommendations

Format your analysis in markdown with clear sections and emoji for better readability.

Content to analyze:

{string.Join("\n\n", allComments.Select(c => c.Body))}";

            var weeklyAnalysis = await _analyzerService.AnalyzeCommentsAsync("reddit", weeklyContent);

            // Generate the HTML email using the new EmailUtils class
            var emailHtml = EmailUtils.GenerateRedditReportEmail(subreddit, cutoffDate, weeklyAnalysis, threadAnalyses, topComments);

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "text/html; charset=utf-8");
            await response.WriteStringAsync(emailHtml);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing Reddit report for r/{Subreddit}", subreddit);
            var response = req.CreateResponse(HttpStatusCode.InternalServerError);
            await response.WriteStringAsync($"Error processing report: {ex.Message}");
            return response;
        }
    }

    private static List<RedditCommentModel> FlattenComments(List<RedditCommentModel> comments)
    {
        var flattened = new List<RedditCommentModel>();
        foreach (var comment in comments)
        {
            flattened.Add(comment);
            if (comment.Replies?.Any() == true)
            {
                flattened.AddRange(FlattenComments(comment.Replies));
            }
        }
        return flattened;
    }
}
