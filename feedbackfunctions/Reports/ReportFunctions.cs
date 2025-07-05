﻿using System.Net;
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
using SharedDump.Services;
using SharedDump.Services.Interfaces;
using SharedDump.Utils;
using Azure.Storage.Blobs;
using FeedbackFunctions.Utils;
using FeedbackFunctions.Services;
using FeedbackFunctions.Services.Reports;
using FeedbackFunctions.Services.Authentication;
using FeedbackFunctions.Middleware;
using FeedbackFunctions.Extensions;
using FeedbackFunctions.Attributes;

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
    private readonly IRedditService _redditService;
    private readonly IGitHubService _githubService;
    private readonly IFeedbackAnalyzerService _analyzerService;
    private readonly IConfiguration _configuration;
    private readonly IReportCacheService _cacheService;
    private readonly FeedbackFunctions.Middleware.AuthenticationMiddleware _authMiddleware;
    private const string ContainerName = "reports";
    private readonly BlobContainerClient _containerClient;
    private readonly ReportGenerator _reportGenerator;
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    /// <summary>
    /// Initializes a new instance of the ReportingFunctions class
    /// </summary>
    /// <param name="logger">Logger for diagnostic information</param>
    /// <param name="configuration">Application configuration</param>
    /// <param name="redditService">Reddit service for subreddit operations</param>
    /// <param name="githubService">GitHub service for repository operations</param>
    /// <param name="analyzerService">Feedback analyzer service for AI-powered analysis</param>
    /// <param name="cacheService">Report cache service for in-memory caching</param>
    /// <param name="authMiddleware">Authentication middleware for request validation</param>
    public ReportingFunctions(
        ILogger<ReportingFunctions> logger,
        IConfiguration configuration,
        IRedditService redditService,
        IGitHubService githubService,
        IFeedbackAnalyzerService analyzerService,
        IReportCacheService cacheService,
        FeedbackFunctions.Middleware.AuthenticationMiddleware authMiddleware)
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
        _redditService = redditService;
        _githubService = githubService;
        _analyzerService = analyzerService;
        _cacheService = cacheService;
        _authMiddleware = authMiddleware;
        
        // Initialize blob container
        var storageConnection = _configuration["ProductionStorage"] ?? throw new InvalidOperationException("Production storage connection string not configured");
        var serviceClient = new BlobServiceClient(storageConnection);
        _containerClient = serviceClient.GetBlobContainerClient(ContainerName);
        _containerClient.CreateIfNotExists();

        // Initialize report generator
        _reportGenerator = new ReportGenerator(_logger, _redditService, _githubService, _analyzerService, _containerClient, _cacheService);
    }

  

    /// <summary>
    /// Gets a report by its ID
    /// </summary>
    /// <param name="req">HTTP request containing the report ID</param>
    /// <returns>HTTP response with the full report data</returns>
    [Function("GetReport")]
    public async Task<HttpResponseData> GetReport(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "Report/{id}")] HttpRequestData req,
        string id)
    {
        _logger.LogInformation("Getting report with ID: {Id}", id);

        try
        {
            // Try to get from cache first
            var report = await _cacheService.GetReportAsync(id);
            
            if (report == null)
            {
                _logger.LogWarning("Report with ID {Id} not found", id);
                var notFoundResponse = req.CreateResponse(HttpStatusCode.NotFound);
                await notFoundResponse.WriteStringAsync($"Report with ID {id} not found");
                return notFoundResponse;
            }

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
    }

     /// <summary>
    /// Get reports for the authenticated user
    /// </summary>
    [Function("GetUserReports")]
    [Authorize]
    public async Task<HttpResponseData> GetUserReports(
        [HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequestData req)
    {
        _logger.LogInformation("Getting reports for authenticated user");

        // Authenticate the request
        var (user, authErrorResponse) = await req.AuthenticateAsync(_authMiddleware);
        if (authErrorResponse != null)
            return authErrorResponse;

        if (user == null)
        {
            var errorResponse = req.CreateResponse(HttpStatusCode.Unauthorized);
            await errorResponse.WriteStringAsync("User authentication failed");
            return errorResponse;
        }

        try
        {
            // Get user's report requests instead of parsing from request body
            // This ensures we only return reports for sources the user has actually requested
            var userReportRequests = new List<ReportRequestModel>();
            
            // Connect to user requests table
            var storageConnection = _configuration["ProductionStorage"] ?? throw new InvalidOperationException("Production storage connection string not configured");
            var tableServiceClient = new Azure.Data.Tables.TableServiceClient(storageConnection);
            var userRequestsTableClient = tableServiceClient.GetTableClient("userreportrequests");

            await foreach (var entity in userRequestsTableClient.QueryAsync<UserReportRequestModel>(
                filter: $"PartitionKey eq '{user.UserId}'"))
            {
                // Convert user request to ReportRequestModel for consistency
                userReportRequests.Add(new ReportRequestModel
                {
                    Type = entity.Type,
                    Subreddit = entity.Subreddit,
                    Owner = entity.Owner,
                    Repo = entity.Repo
                });
            }

            if (!userReportRequests.Any())
            {
                var emptyResponse = req.CreateResponse(HttpStatusCode.OK);
                emptyResponse.Headers.Add("Content-Type", "application/json");
                await emptyResponse.WriteStringAsync(JsonSerializer.Serialize(new { reports = Array.Empty<object>() }));
                return emptyResponse;
            }

            // Get all reports from cache
            var allReports = await _cacheService.GetReportsAsync();
            var matchingReports = new List<object>();

            foreach (var report in allReports)
            {
                // Check if this report matches any of the user's requests
                var matchesRequest = userReportRequests.Any(userReq =>
                    userReq.Type == report.Source &&
                    ((userReq.Type == "reddit" && userReq.Subreddit == report.SubSource) ||
                     (userReq.Type == "github" && $"{userReq.Owner}/{userReq.Repo}" == report.SubSource)));

                if (matchesRequest)
                {
                    matchingReports.Add(new
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

            // Sort by generation date (newest first)
            matchingReports = matchingReports
                .OrderByDescending(r => ((dynamic)r).generatedAt)
                .ToList();

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json");
            await response.WriteStringAsync(JsonSerializer.Serialize(new { reports = matchingReports }));
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting reports for user {UserId}", user.UserId);
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync($"Error getting user reports: {ex.Message}");
            return errorResponse;
        }
    }
}
