using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SharedDump.Models.Reddit;
using SharedDump.Models.GitHub;
using SharedDump.Models.Reports;
using SharedDump.AI;
using SharedDump.Utils;
using Azure.Storage.Blobs;

namespace FeedbackFunctions;

/// <summary>
/// Azure Functions for generating reports based on feedback data
/// </summary>
/// <remarks>
/// This class contains functions that generate comprehensive reports
/// by analyzing feedback from various platforms. The reports are typically
/// formatted in markdown and may include sentiment analysis, trends, and key insights.
/// </remarks>
public class ReportingFunctions
{
    private readonly ILogger<ReportingFunctions> _logger;
    private readonly RedditService _redditService;
    private readonly GitHubService _githubService;
    private readonly IFeedbackAnalyzerService _analyzerService;
    private readonly IConfiguration _configuration;
    private const string ContainerName = "reports";
    private readonly BlobContainerClient _containerClient;

    /// <summary>
    /// Initializes a new instance of the ReportingFunctions class
    /// </summary>
    /// <param name="logger">Logger for diagnostic information</param>
    /// <param name="configuration">Application configuration</param>
    /// <param name="httpClientFactory">HTTP client factory for creating named clients</param>
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

        var githubToken = _configuration["GitHub:AccessToken"] ?? throw new InvalidOperationException("GitHub access token not configured");
        _githubService = new GitHubService(githubToken, httpClientFactory.CreateClient("GitHub"));

        var endpoint = _configuration["Azure:OpenAI:Endpoint"] ?? throw new InvalidOperationException("Azure OpenAI endpoint not configured");
        var apiKey = _configuration["Azure:OpenAI:ApiKey"] ?? throw new InvalidOperationException("Azure OpenAI API key not configured");
        var deployment = _configuration["Azure:OpenAI:Deployment"] ?? throw new InvalidOperationException("Azure OpenAI deployment name not configured");
        _analyzerService = new FeedbackAnalyzerService(endpoint, apiKey, deployment);
        
        // Initialize blob container
        var storageConnection = _configuration["AzureWebJobsStorage"] ?? throw new InvalidOperationException("Storage connection string not configured");
        var serviceClient = new BlobServiceClient(storageConnection);
        _containerClient = serviceClient.GetBlobContainerClient(ContainerName);
        _containerClient.CreateIfNotExists();
    }

    /// <summary>
    /// Generates a comprehensive report of a subreddit's threads and comments
    /// </summary>
    /// <param name="req">HTTP request with query parameters</param>
    /// <returns>HTTP response with a markdown-formatted report</returns>
    /// <remarks>
    /// Query parameters:
    /// - subreddit: Required. The name of the subreddit to analyze
    /// - days: Optional. Number of days of history to analyze (default: 7)
    /// - limit: Optional. Maximum number of threads to analyze (default: 25)
    /// - sort: Optional. Sort method for threads ("hot", "new", "top", etc.)
    /// 
    /// The function fetches Reddit threads, analyzes their content using AI,
    /// and returns a markdown-formatted report with insights, trends, and key takeaways.
    /// </remarks>
    /// <example>
    /// GET /api/RedditReport?subreddit=dotnet&amp;days=30&amp;limit=50&amp;sort=top
    /// </example>
    [Function("RedditReport")]
    public async Task<HttpResponseData> RedditReport(
        [HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequestData req)
    {
        var startTime = DateTime.UtcNow;
        _logger.LogInformation("Starting Reddit Report processing for URL {RequestUrl}", req.Url);

        var queryParams = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
        var subreddit = queryParams["subreddit"];

        if (string.IsNullOrEmpty(subreddit))
        {
            _logger.LogWarning("Reddit Report request rejected - missing subreddit parameter");
            var response = req.CreateResponse(HttpStatusCode.BadRequest);
            await response.WriteStringAsync("Subreddit parameter is required");
            return response;
        }

        try
        {
            _logger.LogInformation("Fetching threads for subreddit r/{Subreddit}", subreddit);
            
            var cutoffDate = DateTimeOffset.UtcNow.AddDays(-7);
            _logger.LogDebug("Using cutoff date of {CutoffDate} for thread retrieval", cutoffDate);
            
            var threads = await _redditService.GetSubredditThreadsBasicInfo(subreddit, "hot", cutoffDate);
            _logger.LogInformation("Retrieved {ThreadCount} threads from r/{Subreddit}", threads.Count, subreddit);

            var topThreads = threads
                .OrderByDescending(t => (t.NumComments * 0.7) + (t.Score * 0.3))
                .Take(5)
                .ToList();
            _logger.LogInformation("Selected top {TopThreadCount} threads for detailed analysis", topThreads.Count);

            // Create tasks for parallel processing of each thread
            var threadTasks = topThreads.Select(async thread =>
            {
                _logger.LogDebug("Fetching full thread details for thread {ThreadId}: {ThreadTitle}", thread.Id, thread.Title);
                var fullThread = await _redditService.GetThreadWithComments(thread.Id);
                
                var flatComments = FlattenComments(fullThread.Comments);

                _logger.LogInformation("Thread {ThreadId} has {CommentCount} comments", thread.Id, flatComments.Count);
                
                var threadContent = $"Title: {fullThread.Title}\n\nContent: {fullThread.SelfText}\n\nComments:\n";
                threadContent += string.Join("\n", flatComments.Select(c => $"Comment by {c.Author}: {c.Body}"));

                var customPrompt = @"Analyze this Reddit thread and provide a concise summary in 3 short sections with use of emojis throughotu to add visual flair:

1. Executive Summary: Brief overview of the main topic and key points (1-3 sentences)
2. Key Insights: Most important takeaways or findings (2-4 bullet points)
3. Sentiment Analysis: Overall mood of the discussion (1-2 sentence2)

Keep each section very brief and focused. Total analysis should be no more than three short paragraphs. Format in markdown.";

                _logger.LogDebug("Analyzing thread {ThreadId}", thread.Id);
                var threadAnalysis = await _analyzerService.AnalyzeCommentsAsync("reddit", threadContent, customPrompt);
              
                _logger.LogDebug("Completed analysis for thread {ThreadId}", thread.Id);

                return (Thread: fullThread, Analysis: threadAnalysis, Comments: flatComments);
            }).ToList();

            // Wait for all thread processing to complete
            var results = await Task.WhenAll(threadTasks);

            var threadAnalyses = results.Select(r => (r.Thread, r.Analysis)).ToList();
            var allComments = results.SelectMany(r => r.Comments).ToList();

            _logger.LogInformation("Processing top comments from {TotalCommentCount} total comments", allComments.Count);
            
            // Enhance top comments with thread information
            var topComments = allComments
                .OrderByDescending(c => c.Score)
                .Take(5)
                .Select(comment =>
                {
                    // Find the parent thread for this comment
                    var parentThread = results.First(r => r.Comments.Any(c => c.Id == comment.Id)).Thread;
                    return new TopCommentInfo(
                        Comment: comment,
                        Thread: parentThread,
                        CommentUrl: $"https://reddit.com{parentThread.Permalink}{comment.Id}"
                    );
                })
                .ToList();

            _logger.LogInformation("Generating weekly summary analysis for r/{Subreddit}", subreddit);

            var customWeeklyContentPrompt = $@"Analyze all of the top posts of the last week of Reddit discussions from r/{subreddit} this is a list of all of the comment sfrom all of these top threads of various topics. provide a concise summary in 3 short sections with use of emojis to add visual flair througout:

1. Executive Summary: Brief overview of the main topic and key points (1-3 sentences)
2. Key Insights or Trends: Most important takeaways or findings (2-4 bullet points)
3. Sentiment Analysis: Overall mood of the discussion (1-2 sentence)

Keep each section very brief and focused. Total analysis should be no more than three short paragraphs. Format in markdown.";
            var weeklyContent = string.Join("\n\n", allComments.Select(c => c.Body));

            var weeklyAnalysis = await _analyzerService.AnalyzeCommentsAsync("reddit", weeklyContent, customWeeklyContentPrompt);
            _logger.LogDebug("Weekly analysis completed");

            _logger.LogInformation("Creating report model");
            var report = new ReportModel
            {
                Source = "reddit",
                SubSource = subreddit,
                ThreadCount = topThreads.Count,
                CommentCount = allComments.Count,
                CutoffDate = cutoffDate.UtcDateTime
            };

            _logger.LogInformation("Generating HTML email report");
            var emailHtml = EmailUtils.GenerateRedditReportEmail(
                subreddit, 
                cutoffDate, 
                weeklyAnalysis, 
                threadAnalyses, 
                topComments,
                report.Id.ToString());

            var processingTime = DateTime.UtcNow - startTime;
            _logger.LogInformation("Reddit Report processing completed for r/{Subreddit} in {ProcessingTime:c}. Analyzed {ThreadCount} threads and {CommentCount} comments", 
                subreddit, processingTime, topThreads.Count, allComments.Count);
            
            report.HtmlContent = emailHtml;

            // Save to blob storage
            var blobClient = _containerClient.GetBlobClient($"{report.Id}.json");
            var reportJson = JsonSerializer.Serialize(report);
            await using var ms = new MemoryStream(Encoding.UTF8.GetBytes(reportJson));
            await blobClient.UploadAsync(ms, overwrite: true);

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "text/html; charset=utf-8");
            await response.WriteStringAsync(emailHtml);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing Reddit report for r/{Subreddit}. Processing time: {ProcessingTime:c}", 
                subreddit, DateTime.UtcNow - startTime);
            var response = req.CreateResponse(HttpStatusCode.InternalServerError);
            await response.WriteStringAsync($"Error processing report: {ex.Message}");
            return response;
        }
    }

    /// <summary>
    /// Generates a comprehensive report of GitHub repository issues
    /// </summary>
    /// <param name="req">HTTP request with query parameters</param>
    /// <returns>HTTP response with a markdown-formatted report</returns>
    /// <remarks>
    /// Query parameters:
    /// - repo: Required. The repository in format "owner/name" (e.g., "microsoft/vscode")
    /// - days: Optional. Number of days of history to analyze (default: 7)
    /// 
    /// The function fetches GitHub issues from the last week, analyzes their content using AI,
    /// and returns an HTML-formatted report with insights, trends, and key takeaways.
    /// </remarks>
    /// <example>
    /// GET /api/GitHubIssuesReport?repo=microsoft/vscode&amp;days=7
    /// </example>
    [Function("GitHubIssuesReport")]
    public async Task<HttpResponseData> GitHubIssuesReport(
        [HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequestData req)
    {
        var startTime = DateTime.UtcNow;
        _logger.LogInformation("Starting GitHub Issues Report processing for URL {RequestUrl}", req.Url);

        var queryParams = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
        var repo = queryParams["repo"];
        var daysParam = queryParams["days"];
        var days = int.TryParse(daysParam, out var parsedDays) ? parsedDays : 7;

        if (string.IsNullOrEmpty(repo))
        {
            _logger.LogWarning("GitHub Issues Report request rejected - missing repo parameter");
            var missingRepoResponse = req.CreateResponse(HttpStatusCode.BadRequest);
            await missingRepoResponse.WriteStringAsync("Repository parameter is required (format: owner/name)");
            return missingRepoResponse;
        }

        var repoParts = repo.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (repoParts.Length != 2)
        {
            _logger.LogWarning("GitHub Issues Report request rejected - invalid repo format: {Repo}", repo);
            var invalidFormatResponse = req.CreateResponse(HttpStatusCode.BadRequest);
            await invalidFormatResponse.WriteStringAsync("Repository parameter must be in format 'owner/name'");
            return invalidFormatResponse;
        }

        var repoOwner = repoParts[0];
        var repoName = repoParts[1];

        try
        {
            _logger.LogInformation("Fetching issues for repository {RepoOwner}/{RepoName} for last {Days} days", repoOwner, repoName, days);
            
            var startDate = DateTimeOffset.UtcNow.AddDays(-days);
            var endDate = DateTimeOffset.UtcNow;
            
            // Check if repository is valid
            if (!await _githubService.CheckRepositoryValid(repoOwner, repoName))
            {
                _logger.LogWarning("Repository {RepoOwner}/{RepoName} not found or not accessible", repoOwner, repoName);
                var badRequestResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequestResponse.WriteStringAsync("Repository not found or not accessible");
                return badRequestResponse;
            }

            var issues = await _githubService.GetRecentIssuesForReportAsync(repoOwner, repoName, days);
            _logger.LogInformation("Retrieved {IssueCount} issues from {RepoOwner}/{RepoName}", issues.Count, repoOwner, repoName);

            if (issues.Count == 0)
            {
                _logger.LogInformation("No issues found for {RepoOwner}/{RepoName} in the last {Days} days", repoOwner, repoName, days);
                var noIssuesResponse = req.CreateResponse(HttpStatusCode.OK);
                noIssuesResponse.Headers.Add("Content-Type", "text/html; charset=utf-8");
                await noIssuesResponse.WriteStringAsync($"<html><body><h1>No Issues Found</h1><p>No issues were found for {repo} in the last {days} days.</p></body></html>");
                return noIssuesResponse;
            }

            // Rank issues by engagement (comments + reactions)
            var topIssues = GitHubIssuesUtils.RankIssuesByEngagement(issues, 5);
            _logger.LogInformation("Selected top {TopIssueCount} issues for detailed analysis", topIssues.Count);

            // Create tasks for parallel processing of each issue
            var issueTasks = topIssues.Select(async (issue, index) =>
            {
                _logger.LogDebug("Analyzing issue {IssueId}: {IssueTitle}", issue.Id, issue.Title);
                
                var issueContent = $"Title: {issue.Title}\n\nState: {issue.State}\n\nComments: {issue.CommentsCount}\nReactions: {issue.ReactionsCount}\n\nLabels: {string.Join(", ", issue.Labels)}";

                var customPrompt = @"Analyze this GitHub issue and provide a concise summary in 3 short sections with use of emojis throughout to add visual flair:

1. Executive Summary: Brief overview of the issue and key points (1-3 sentences)
2. Key Insights: Most important takeaways or findings about this issue (2-4 bullet points)
3. Impact Assessment: Why this issue is significant and its overall sentiment (1-2 sentences)

Keep each section very brief and focused. Total analysis should be no more than three short paragraphs. Format in markdown.";

                var issueAnalysis = await _analyzerService.AnalyzeCommentsAsync("github", issueContent, customPrompt);
                _logger.LogDebug("Completed analysis for issue {IssueId}", issue.Id);

                var issueDetail = new GithubIssueDetail
                {
                    Summary = issue,
                    DeepAnalysis = issueAnalysis,
                    Body = "", // We don't have the full body in the summary
                    Comments = Array.Empty<GithubCommentModel>() // We don't fetch full comments for the report
                };

                return new TopGitHubIssueInfo(issueDetail, index + 1, issue.CommentsCount + issue.ReactionsCount);
            }).ToList();

            // Wait for all issue processing to complete
            var topIssueInfos = await Task.WhenAll(issueTasks);

            _logger.LogInformation("Generating overall trend analysis for {RepoOwner}/{RepoName}", repoOwner, repoName);

            // Generate overall analysis based on all issue titles and metadata
            var overallAnalysis = GitHubIssuesUtils.AnalyzeTitleTrends(issues);
            var customWeeklyPrompt = $@"Analyze these GitHub issues statistics and trends from {repo} repository. Provide a concise summary in 3 short sections with use of emojis to add visual flair throughout:

1. Executive Summary: Brief overview of the main issues and trends (1-3 sentences)
2. Key Insights or Trends: Most important takeaways from the issue patterns (2-4 bullet points)
3. Repository Health: Overall assessment of the repository activity and community engagement (1-2 sentences)

Statistics: {overallAnalysis}

Keep each section very brief and focused. Total analysis should be no more than three short paragraphs. Format in markdown.";

            var enhancedOverallAnalysis = await _analyzerService.AnalyzeCommentsAsync("github", overallAnalysis, customWeeklyPrompt);
            _logger.LogDebug("Overall analysis completed");

            _logger.LogInformation("Creating GitHub issues report model");
            var report = new ReportModel
            {
                Source = "github",
                SubSource = repo,
                ThreadCount = topIssues.Count,
                CommentCount = issues.Sum(i => i.CommentsCount),
                CutoffDate = startDate.UtcDateTime
            };

            _logger.LogInformation("Generating HTML email report");
            var emailHtml = GitHubIssuesUtils.GenerateGitHubIssuesReportEmail(
                repo,
                startDate,
                endDate,
                enhancedOverallAnalysis,
                topIssueInfos.ToList(),
                report.Id.ToString());

            var processingTime = DateTime.UtcNow - startTime;
            _logger.LogInformation("GitHub Issues Report processing completed for {RepoOwner}/{RepoName} in {ProcessingTime:c}. Analyzed {IssueCount} issues", 
                repoOwner, repoName, processingTime, issues.Count);
            
            report.HtmlContent = emailHtml;

            // Save to blob storage
            var blobClient = _containerClient.GetBlobClient($"{report.Id}.json");
            var reportJson = JsonSerializer.Serialize(report);
            await using var ms = new MemoryStream(Encoding.UTF8.GetBytes(reportJson));
            await blobClient.UploadAsync(ms, overwrite: true);

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "text/html; charset=utf-8");
            await response.WriteStringAsync(emailHtml);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing GitHub Issues report for {RepoOwner}/{RepoName}. Processing time: {ProcessingTime:c}", 
                repoOwner, repoName, DateTime.UtcNow - startTime);
            var response = req.CreateResponse(HttpStatusCode.InternalServerError);
            await response.WriteStringAsync($"Error processing report: {ex.Message}");
            return response;
        }
    }    /// <summary>
    /// Gets a report by its ID
    /// </summary>
    /// <param name="req">HTTP request containing the report ID</param>
    /// <returns>HTTP response with the full report data</returns>
    [Function("GetReport")]
    public async Task<HttpResponseData> GetReport(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "Report/{id}")] HttpRequestData req,
        string id)
    {
        _logger.LogInformation("Getting Reddit report with ID: {Id}", id);

        try
        {
            var blobClient = _containerClient.GetBlobClient($"{id}.json");
            if (!await blobClient.ExistsAsync())
            {
                _logger.LogWarning("Report with ID {Id} not found", id);
                var notFoundResponse = req.CreateResponse(HttpStatusCode.NotFound);
                await notFoundResponse.WriteStringAsync($"Report with ID {id} not found");
                return notFoundResponse;
            }

            var blobContent = await blobClient.DownloadContentAsync();
            var report = JsonSerializer.Deserialize<ReportModel>(blobContent.Value.Content);

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json");
            await response.WriteStringAsync(JsonSerializer.Serialize(report));
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving report with ID {Id}", id);
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync($"Error retrieving report: {ex.Message}");
            return errorResponse;
        }
    }    /// <summary>
    /// Lists all reports with basic metadata
    /// </summary>
    /// <param name="req">HTTP request</param>
    /// <returns>HTTP response with a list of report summaries</returns>
    /// <remarks>
    /// Query parameters:
    /// - source: Optional. Filter reports by source (e.g., "reddit")
    /// - subsource: Optional. Filter reports by subsource (e.g., "dotnet")
    /// 
    /// If no parameters are provided, returns all reports (backward compatible).
    /// Parameters can be used individually or in combination.
    /// </remarks>
    [Function("ListReports")]
    public async Task<HttpResponseData> ListReports(
        [HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequestData req)
    {
        var queryParams = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
        var sourceFilter = queryParams["source"];
        var subsourceFilter = queryParams["subsource"];
        
        var filterMessage = sourceFilter == null && subsourceFilter == null 
            ? "all reports" 
            : $"reports (source: {sourceFilter ?? "any"}, subsource: {subsourceFilter ?? "any"})";
            
        _logger.LogInformation("Listing {FilterMessage}", filterMessage);

        try
        {
            var reports = new List<object>();
            await foreach (var blob in _containerClient.GetBlobsAsync())
            {
                var blobClient = _containerClient.GetBlobClient(blob.Name);
                var content = await blobClient.DownloadContentAsync();
                var report = JsonSerializer.Deserialize<ReportModel>(content.Value.Content);

                if (report != null)
                {
                    // Apply filtering if parameters are provided
                    var matchesSource = string.IsNullOrEmpty(sourceFilter) || 
                                       string.Equals(report.Source, sourceFilter, StringComparison.OrdinalIgnoreCase);
                    var matchesSubsource = string.IsNullOrEmpty(subsourceFilter) || 
                                          string.Equals(report.SubSource, subsourceFilter, StringComparison.OrdinalIgnoreCase);
                    
                    if (matchesSource && matchesSubsource)
                    {
                        reports.Add(new
                        {
                            id = report.Id,
                            source = report.Source,
                            subSource = report.SubSource,
                            generatedAt = report.GeneratedAt,
                            threadCount = report.ThreadCount,
                            commentCount = report.CommentCount,
                            cutoffDate = report.CutoffDate
                        });
                    }
                }
            }

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json");
            await response.WriteStringAsync(JsonSerializer.Serialize(new { reports }));
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing reports");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync($"Error listing reports: {ex.Message}");
            return errorResponse;
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
