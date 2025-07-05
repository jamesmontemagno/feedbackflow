using System;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using FeedbackFunctions.Services.Account;
using SharedDump.Models.Account;

namespace FeedbackFunctions.Middleware
{
    public class UsageValidationMiddleware : IFunctionsWorkerMiddleware
    {
        public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
        {
            // Map function names to UsageType for validation
            var usageTypeMap = new System.Collections.Generic.Dictionary<string, UsageType>(System.StringComparer.OrdinalIgnoreCase)
            {
                // Analysis functions
                { "AnalyzeComments", UsageType.Analysis },
                
                // Feed query functions
                { "GetGitHubFeedback", UsageType.FeedQuery },
                { "GetHackerNewsFeedback", UsageType.FeedQuery },
                { "GetYouTubeFeedback", UsageType.FeedQuery },
                { "GetRedditFeedback", UsageType.FeedQuery },
                { "GetDevBlogsFeedback", UsageType.FeedQuery },
                { "GetTwitterFeedback", UsageType.FeedQuery },
                { "GetBlueSkyFeedback", UsageType.FeedQuery },
                { "GetRecentYouTubeVideos", UsageType.FeedQuery },
                { "GetTrendingRedditThreads", UsageType.FeedQuery },
                { "SearchHackerNewsArticles", UsageType.FeedQuery },
                
                // Report functions
                { "AddUserReportRequest", UsageType.ReportCreated },
                { "RemoveUserReportRequest", UsageType.ReportDeleted },
                
                // Note: Some functions like GetSharedAnalysis, GetCurrentUser, etc. don't require usage validation
                // Timer functions and admin functions also don't require usage validation
            };

            if (usageTypeMap.TryGetValue(context.FunctionDefinition.Name, out var usageType))
            {
                var logger = context.InstanceServices.GetService<ILogger<UsageValidationMiddleware>>();
                var usageService = context.InstanceServices.GetService<IAccountLimitsService>();
                var userId = context.BindingContext.BindingData["userId"]?.ToString();
                if (string.IsNullOrEmpty(userId))
                {
                    logger?.LogWarning("No userId found for usage validation.");
                    await next(context);
                    return;
                }
                var result = await usageService!.ValidateUsageAsync(userId, usageType);
                if (!result.IsWithinLimit)
                {
                    logger?.LogWarning($"Usage limit exceeded for user {userId} and type {usageType}");
                    var response = context.GetHttpResponseData();
                    if (response != null)
                    {
                        response.StatusCode = System.Net.HttpStatusCode.TooManyRequests;
                        // Write JSON manually since WriteAsJsonAsync may not exist
                        var json = System.Text.Json.JsonSerializer.Serialize(new
                        {
                            ErrorCode = "USAGE_LIMIT_EXCEEDED",
                            Message = result.ErrorMessage ?? "Usage limit exceeded.",
                            LimitType = result.UsageType,
                            CurrentUsage = result.CurrentUsage,
                            Limit = result.Limit,
                            ResetDate = result.ResetDate,
                            CurrentTier = result.CurrentTier,
                            UpgradeUrl = result.UpgradeUrl
                        });
                        using var writer = new System.IO.StreamWriter(response.Body);
                        writer.Write(json);
                        writer.Flush();
                        return;
                    }
                }
            }
            await next(context);
        }
    }
}
