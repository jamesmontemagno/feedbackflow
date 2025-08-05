using System.Text;
using System.Text.Json;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SharedDump.AI;
using SharedDump.Models.Reddit;
using SharedDump.Models.GitHub;
using SharedDump.Models.Reports;
using SharedDump.Services.Interfaces;
using SharedDump.Utils;
using FeedbackFunctions.Services;
using FeedbackFunctions.Services.Reports;

namespace FeedbackFunctions.Utils;

/// <summary>
/// Utility class for generating various types of reports
/// </summary>
public class ReportGenerator
{
    private readonly ILogger _logger;
    private readonly IRedditService _redditService;
    private readonly IGitHubService _githubService;
    private readonly IFeedbackAnalyzerService _analyzerService;
    private readonly BlobContainerClient _containerClient;
    private readonly BlobContainerClient _summaryContainerClient;
    private readonly IReportCacheService? _cacheService;
    private readonly IConfiguration _configuration;
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    public ReportGenerator(
        ILogger logger,
        IRedditService redditService,
        IGitHubService githubService,
        IFeedbackAnalyzerService analyzerService,
        BlobServiceClient blobServiceClient,
        IConfiguration configuration,
        IReportCacheService? cacheService = null)
    {
        _logger = logger;
        _redditService = redditService;
        _githubService = githubService;
        _analyzerService = analyzerService;
        _configuration = configuration;
        _cacheService = cacheService;
        
        // Initialize both blob container clients
        _containerClient = blobServiceClient.GetBlobContainerClient("reports");
        _containerClient.CreateIfNotExists();
        
        _summaryContainerClient = blobServiceClient.GetBlobContainerClient("reports-summary");
        _summaryContainerClient.CreateIfNotExists();
    }

    /// <summary>
    /// Generates a comprehensive Reddit report for a subreddit
    /// </summary>
    /// <param name="subreddit">The subreddit name to analyze</param>
    /// <param name="cutoffDate">The cutoff date for threads (default: 7 days ago)</param>
    /// <returns>The generated report model with HTML content</returns>
    public async Task<ReportModel> GenerateRedditReportAsync(string subreddit, DateTimeOffset? cutoffDate = null, bool? storeToBlob = false)
    {
        var startTime = DateTime.UtcNow;
        var actualCutoffDate = cutoffDate ?? DateTimeOffset.UtcNow.AddDays(-7);
        
        _logger.LogInformation("Starting Reddit Report generation for r/{Subreddit}", subreddit);

        try
        {
            _logger.LogInformation("Fetching threads for subreddit r/{Subreddit}", subreddit);
            _logger.LogDebug("Using cutoff date of {CutoffDate} for thread retrieval", actualCutoffDate);
            
            var threads = await _redditService.GetSubredditThreadsBasicInfo(subreddit, "hot", actualCutoffDate);
            _logger.LogInformation("Retrieved {ThreadCount} threads from r/{Subreddit}", threads.Count, subreddit);

            // Fetch subreddit statistics
            _logger.LogInformation("Fetching subreddit statistics for r/{Subreddit}", subreddit);
            var subredditInfo = await _redditService.GetSubredditInfo(subreddit);
            _logger.LogInformation("Retrieved subreddit info: {Subscribers} subscribers", 
                subredditInfo.Subscribers);

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
                        CommentUrl: RedditUrlParser.GenerateCommentUrl(parentThread.Permalink, comment.Id)
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
                CutoffDate = actualCutoffDate.UtcDateTime
            };

            _logger.LogInformation("Generating HTML email report");
            
            // Calculate additional stats
            var totalUpvotes = threads.Sum(t => t.Score);
            var newThreadsCount = threads.Count;
            var totalCommentsCount = threads.Sum(t => t.NumComments);
            
            var emailHtml = EmailUtils.GenerateRedditReportEmail(
                subreddit, 
                actualCutoffDate, 
                weeklyAnalysis, 
                threadAnalyses, 
                topComments,
                report.Id.ToString(),
                subredditInfo, 
                newThreadsCount, 
                totalCommentsCount, 
                totalUpvotes);

            var processingTime = DateTime.UtcNow - startTime;
            _logger.LogInformation("Reddit Report generation completed for r/{Subreddit} in {ProcessingTime:c}. Analyzed {ThreadCount} threads and {CommentCount} comments", 
                subreddit, processingTime, topThreads.Count, allComments.Count);
            
            report.HtmlContent = emailHtml;

            // Save to blob storage using the helper method
            if (storeToBlob.HasValue && storeToBlob.Value)
            {
                await StoreReportAsync(report);
                
                // Also create and store a summary report
                _logger.LogInformation("Generating summary report for r/{Subreddit}", subreddit);
                var summaryReport = new ReportModel
                {
                    Id = report.Id, // Use the same ID for linking
                    Source = report.Source,
                    SubSource = report.SubSource,
                    ThreadCount = report.ThreadCount,
                    CommentCount = report.CommentCount,
                    CutoffDate = report.CutoffDate,
                    GeneratedAt = report.GeneratedAt
                };

                var fullReportUrl = WebUrlHelper.BuildReportUrl(_configuration, report.Id);
                var summaryHtml = EmailUtils.GenerateRedditReportSummary(
                    subreddit, 
                    actualCutoffDate, 
                    weeklyAnalysis, 
                    threadAnalyses,
                    report.Id.ToString(),
                    subredditInfo, 
                    newThreadsCount, 
                    totalCommentsCount, 
                    totalUpvotes,
                    fullReportUrl);
                    
                summaryReport.HtmlContent = summaryHtml;
                await StoreSummaryReportAsync(summaryReport);
                _logger.LogInformation("Successfully stored summary report for r/{Subreddit} with ID {ReportId}", subreddit, summaryReport.Id);
            }

            return report;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating Reddit report for r/{Subreddit}. Processing time: {ProcessingTime:c}", 
                subreddit, DateTime.UtcNow - startTime);
            throw;
        }
    }

    /// <summary>
    /// Generates a comprehensive GitHub report for a repository
    /// </summary>
    /// <param name="owner">The repository owner</param>
    /// <param name="repo">The repository name</param>
    /// <param name="days">Number of days to look back (default: 7)</param>
    /// <returns>The generated report model with HTML content</returns>
    public async Task<ReportModel> GenerateGitHubReportAsync(string owner, string repo, int days = 7, bool? storeToBlob = false)
    {
        var startTime = DateTime.UtcNow;
        var startDate = DateTimeOffset.UtcNow.AddDays(-days);
        var endDate = DateTimeOffset.UtcNow;
        
        _logger.LogInformation("Starting GitHub Issues Report generation for {Owner}/{Repo}", owner, repo);

        try
        {
            _logger.LogInformation("Fetching issues for repository {Owner}/{Repo} for last {Days} days", owner, repo, days);
            
            // Check if repository is valid
            if (!await _githubService.CheckRepositoryValid(owner, repo))
            {
                throw new InvalidOperationException($"Repository {owner}/{repo} not found or not accessible");
            }

            var issues = await _githubService.GetRecentIssuesForReportAsync(owner, repo, days);
            _logger.LogInformation("Retrieved {IssueCount} issues from {Owner}/{Repo}", issues.Count, owner, repo);

            if (issues.Count == 0)
            {
                _logger.LogInformation("No issues found for {Owner}/{Repo} in the last {Days} days", owner, repo, days);
                // Return empty report
                var emptyReport = new ReportModel
                {
                    Source = "github",
                    SubSource = $"{owner}/{repo}",
                    ThreadCount = 0,
                    CommentCount = 0,
                    CutoffDate = startDate.UtcDateTime,
                    HtmlContent = $"<html><body><h1>No Issues Found</h1><p>No issues were found for {owner}/{repo} in the last {days} days.</p></body></html>"
                };

                // Save to blob storage using the helper method
                if (storeToBlob.HasValue && storeToBlob.Value)
                {
                    await StoreReportAsync(emptyReport);
                }

                return emptyReport;
            }

            // Rank issues by engagement (comments + reactions)
            var topIssues = GitHubIssuesUtils.RankIssuesByEngagement(issues, 8);
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

            _logger.LogInformation("Generating overall trend analysis for {Owner}/{Repo}", owner, repo);

            // Generate overall analysis based on all issue titles and metadata
            var overallAnalysis = GitHubIssuesUtils.AnalyzeTitleTrends(issues);
            var customWeeklyPrompt = $@"Analyze these GitHub issues statistics and trends from {owner}/{repo} repository. Provide a concise summary in 3 short sections with use of emojis to add visual flair throughout:

1. Executive Summary: Brief overview of the main issues and trends (1-3 sentences)
2. Key Insights or Trends: Most important takeaways from the issue patterns (2-4 bullet points)
3. Repository Health: Overall assessment of the repository activity and community engagement (1-2 sentences)

Statistics: {overallAnalysis}

Keep each section very brief and focused. Total analysis should be no more than three short paragraphs. Format in markdown.";

            var enhancedOverallAnalysis = await _analyzerService.AnalyzeCommentsAsync("github", overallAnalysis, customWeeklyPrompt);
            _logger.LogDebug("Overall analysis completed");

            // Get oldest important issues that have recent comment activity
            _logger.LogInformation("Fetching oldest important issues with recent activity for {Owner}/{Repo}", owner, repo);
            var oldestImportantIssues = await _githubService.GetOldestImportantIssuesWithRecentActivityAsync(owner, repo, 7, 3);
            _logger.LogInformation("Found {OldestIssueCount} oldest important issues with recent activity", oldestImportantIssues.Count);

            _logger.LogInformation("Creating GitHub issues report model");
            var report = new ReportModel
            {
                Source = "github",
                SubSource = $"{owner}/{repo}",
                ThreadCount = topIssues.Count,
                CommentCount = issues.Sum(i => i.CommentsCount),
                CutoffDate = startDate.UtcDateTime
            };

            _logger.LogInformation("Generating HTML email report");
            var emailHtml = GitHubIssuesUtils.GenerateGitHubIssuesReportEmail(
                $"{owner}/{repo}",
                startDate,
                endDate,
                enhancedOverallAnalysis,
                topIssueInfos.ToList(),
                oldestImportantIssues,
                report.Id.ToString());

            var processingTime = DateTime.UtcNow - startTime;
            _logger.LogInformation("GitHub Issues Report generation completed for {Owner}/{Repo} in {ProcessingTime:c}. Analyzed {IssueCount} issues", 
                owner, repo, processingTime, issues.Count);
            
            report.HtmlContent = emailHtml;

            // Save to blob storage using the helper method
            if (storeToBlob.HasValue && storeToBlob.Value)
            {
                await StoreReportAsync(report);
            }

            return report;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating GitHub Issues report for {Owner}/{Repo}. Processing time: {ProcessingTime:c}", 
                owner, repo, DateTime.UtcNow - startTime);
            throw;
        }
    }

    /// <summary>
    /// Flattens a hierarchical list of Reddit comments into a flat list
    /// </summary>
    /// <param name="comments">The hierarchical comments to flatten</param>
    /// <returns>A flat list of all comments</returns>
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

    /// <summary>
    /// Stores a report in blob storage and cache
    /// </summary>
    /// <param name="report">The report to store</param>
    /// <returns>The blob name where the report was stored</returns>
    public async Task<string> StoreReportAsync(ReportModel report)
    {
        _logger.LogInformation("Storing report {ReportId} to blob storage", report.Id);

        try
        {
            var blobName = $"{report.Id}.json";
            var blobClient = _containerClient.GetBlobClient(blobName);
            
            var reportJson = JsonSerializer.Serialize(report);
            await using var ms = new MemoryStream(Encoding.UTF8.GetBytes(reportJson));
            await blobClient.UploadAsync(ms, overwrite: true);

            // Also cache the report if cache service is available
            if (_cacheService != null)
            {
                await _cacheService.SetReportAsync(report);
                _logger.LogDebug("Cached report {ReportId} after storing to blob", report.Id);
            }

            _logger.LogInformation("Successfully stored report {ReportId} as blob {BlobName}", report.Id, blobName);
            return blobName;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error storing report {ReportId} to blob storage", report.Id);
            throw;
        }
    }

    /// <summary>
    /// Stores a summary report in blob storage
    /// </summary>
    /// <param name="report">The summary report to store</param>
    /// <returns>The blob name where the summary report was stored</returns>
    public async Task<string> StoreSummaryReportAsync(ReportModel report)
    {
        _logger.LogInformation("Storing summary report {ReportId} to blob storage", report.Id);

        try
        {
            var blobName = $"{report.Id}-summary.json";
            var blobClient = _summaryContainerClient.GetBlobClient(blobName);
            
            var reportJson = JsonSerializer.Serialize(report);
            await using var ms = new MemoryStream(Encoding.UTF8.GetBytes(reportJson));
            await blobClient.UploadAsync(ms, overwrite: true);

            _logger.LogInformation("Successfully stored summary report {ReportId} as blob {BlobName}", report.Id, blobName);
            return blobName;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error storing summary report {ReportId} to blob storage", report.Id);
            throw;
        }
    }

    /// <summary>
    /// Gets a recent summary report (within last 24 hours) for the given source and subsource
    /// </summary>
    /// <param name="source">The report source (e.g., "reddit", "github")</param>
    /// <param name="subSource">The report subsource (e.g., subreddit name, repo name)</param>
    /// <returns>The most recent summary report if found within 24 hours, otherwise null</returns>
    public async Task<ReportModel?> GetRecentSummaryReportAsync(string source, string subSource)
    {
        try
        {
            _logger.LogDebug("Checking for recent summary reports with source '{Source}' and subsource '{SubSource}'", source, subSource);
            
            var cutoff = DateTimeOffset.UtcNow.AddHours(-24);
            var recentReport = (ReportModel?)null;
            
            await foreach (var blob in _summaryContainerClient.GetBlobsAsync())
            {
                if (blob.Properties.LastModified >= cutoff)
                {
                    try
                    {
                        var blobClient = _summaryContainerClient.GetBlobClient(blob.Name);
                        var content = await blobClient.DownloadContentAsync();
                        var report = JsonSerializer.Deserialize<ReportModel>(content.Value.Content, _jsonOptions);
                        
                        if (report != null && report.Source == source && report.SubSource == subSource)
                        {
                            if (recentReport == null || report.GeneratedAt > recentReport.GeneratedAt)
                            {
                                recentReport = report;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error reading summary report blob {BlobName}", blob.Name);
                    }
                }
            }

            if (recentReport != null)
            {
                _logger.LogInformation("Found recent summary report {ReportId} generated at {GeneratedAt} for {Source}/{SubSource}", 
                    recentReport.Id, recentReport.GeneratedAt, source, subSource);
            }
            else
            {
                _logger.LogDebug("No recent summary report found for {Source}/{SubSource}", source, subSource);
            }

            return recentReport;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error checking for recent summary reports for {Source}/{SubSource}. Will proceed with generating new report.", source, subSource);
            return null;
        }
    }

    /// <summary>
    /// Processes a report request and stores the generated report
    /// </summary>
    /// <param name="request">The report request to process</param>
    /// <returns>The generated report, or null if processing failed</returns>
    public async Task<ReportModel?> ProcessReportRequestAsync(ReportRequestModel request)
    {
        try
        {
            if (request.Type == "reddit" && !string.IsNullOrEmpty(request.Subreddit))
            {
                _logger.LogInformation("Processing Reddit report for r/{Subreddit}", request.Subreddit);
                var report = await GenerateRedditReportAsync(request.Subreddit, storeToBlob: true);
                _logger.LogInformation("Successfully generated and stored Reddit report for r/{Subreddit} with ID {ReportId}", 
                    request.Subreddit, report.Id);
                return report;
            }
            else if (request.Type == "github" && !string.IsNullOrEmpty(request.Owner) && !string.IsNullOrEmpty(request.Repo))
            {
                _logger.LogInformation("Processing GitHub report for {Owner}/{Repo}", request.Owner, request.Repo);
                var report = await GenerateGitHubReportAsync(request.Owner, request.Repo, storeToBlob: true);
                _logger.LogInformation("Successfully generated and stored GitHub report for {Owner}/{Repo} with ID {ReportId}", 
                    request.Owner, request.Repo, report.Id);
                return report;
            }
            else
            {
                _logger.LogWarning("Invalid or incomplete report request: Type={Type}, Subreddit={Subreddit}, Owner={Owner}, Repo={Repo}", 
                    request.Type, request.Subreddit, request.Owner, request.Repo);
                return null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing report request {RequestId}", request.Id);
            return null;
        }
    }
}
