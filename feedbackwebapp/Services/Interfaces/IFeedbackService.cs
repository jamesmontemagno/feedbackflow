namespace FeedbackWebApp.Services.Interfaces;

/// <summary>
/// Core interface for all feedback services in the application
/// </summary>
/// <remarks>
/// This service handles retrieving, parsing, and analyzing feedback from various sources.
/// It provides methods for getting raw comments and analyzing them to produce insights.
/// </remarks>
/// <example>
/// Basic implementation example:
/// <code>
/// public class MyFeedbackService : IFeedbackService
/// {
///     public async Task&lt;(string rawComments, int commentCount, object? additionalData)&gt; GetComments(CancellationToken cancellationToken = default)
///     {
///         // Retrieve comments from your source
///         var comments = await FetchCommentsFromSource(cancellationToken);
///         return (comments, 1, null);
///     }
///     
///     public async Task&lt;(string markdownResult, object? additionalData)&gt; AnalyzeComments(
///         string comments, int? commentCount = null, object? additionalData = null, CancellationToken cancellationToken = default)
///     {
///         // Analyze the comments
///         var analysis = await ProcessComments(comments, cancellationToken);
///         return (analysis, null);
///     }
///     
///     public async Task&lt;(string markdownResult, object? additionalData)&gt; GetFeedback(CancellationToken cancellationToken = default)
///     {
///         // Get comments and analyze them in one operation
///         var (comments, count, data) = await GetComments(cancellationToken);
///         return await AnalyzeComments(comments, count, data, cancellationToken);
///     }
/// }
/// </code>
/// </example>
public interface IFeedbackService : IDisposable
{    /// <summary>
	 /// Gets raw comments directly from the source
	 /// </summary>
	 /// <param name="cancellationToken">Cancellation token to cancel the operation</param>
	 /// <returns>The raw comments, total number of comments found, and any additional data</returns>
	Task<(string rawComments, int commentCount, object? additionalData)> GetComments(CancellationToken cancellationToken = default);

	/// <summary>
	/// Analyzes comments to produce insights
	/// </summary>
	/// <param name="comments">The comments to analyze</param>
	/// <param name="commentCount">Optional total number of comments</param>
	/// <param name="additionalData">Optional additional data to help with analysis</param>
	/// <param name="cancellationToken">Cancellation token to cancel the operation</param>
	/// <returns>Analysis result in markdown format and any additional processed data</returns>
	Task<(string markdownResult, object? additionalData)> AnalyzeComments(
        string comments, 
        int? commentCount = null, 
        object? additionalData = null, 
        CancellationToken cancellationToken = default);

	/// <summary>
	/// Gets and analyzes feedback in a single operation
	/// </summary>
	/// <param name="cancellationToken">Cancellation token to cancel the operation</param>
	/// <returns>Analysis result in markdown format and any additional data</returns>
	Task<(string markdownResult, object? additionalData)> GetFeedback(CancellationToken cancellationToken = default);
}

/// <summary>
/// YouTube-specific feedback service for analyzing YouTube comments
/// </summary>
/// <remarks>
/// This service handles retrieving and analyzing comments from YouTube videos.
/// </remarks>
public interface IYouTubeFeedbackService : IFeedbackService { }

/// <summary>
/// Hacker News-specific feedback service for analyzing Hacker News comments
/// </summary>
/// <remarks>
/// This service handles retrieving and analyzing comments from Hacker News stories.
/// </remarks>
public interface IHackerNewsFeedbackService : IFeedbackService { }

/// <summary>
/// GitHub-specific feedback service for analyzing GitHub issues, PRs, and discussions
/// </summary>
/// <remarks>
/// This service handles retrieving and analyzing comments from GitHub repositories.
/// </remarks>
public interface IGitHubFeedbackService : IFeedbackService { }

/// <summary>
/// Reddit-specific feedback service for analyzing Reddit threads
/// </summary>
/// <remarks>
/// This service handles retrieving and analyzing comments from Reddit posts.
/// </remarks>
public interface IRedditFeedbackService : IFeedbackService { }

/// <summary>
/// Twitter-specific feedback service for analyzing Twitter threads
/// </summary>
/// <remarks>
/// This service handles retrieving and analyzing comments from Twitter threads.
/// </remarks>
public interface ITwitterFeedbackService : IFeedbackService { }

/// <summary>
/// Auto-detected data source feedback service
/// </summary>
/// <remarks>
/// Automatically determines the appropriate feedback service based on the input.
/// </remarks>
public interface IAutoDataSourceFeedbackService : IFeedbackService
{
	// No need for GetFeedback declaration since it's inherited from IFeedbackService
}

/// <summary>
/// BlueSky-specific feedback service for analyzing BlueSky posts
/// </summary>
/// <remarks>
/// This service handles retrieving and analyzing comments from BlueSky.
/// </remarks>
public interface IBlueSkyFeedbackService : IFeedbackService
{
}

/// <summary>
/// Manual feedback service for analyzing user-provided content
/// </summary>
/// <remarks>
/// This service allows users to provide their own content and custom prompts for analysis.
/// </remarks>
/// <example>
/// <code>
/// var service = new ManualFeedbackService(http, config, userSettings, "My content to analyze", "Custom prompt");
/// var result = await service.GetFeedback();
/// </code>
/// </example>
public interface IManualFeedbackService : IFeedbackService
{
	/// <summary>
	/// Custom prompt to use for analysis
	/// </summary>
	string CustomPrompt { get; set; }
	
	/// <summary>
	/// Content to be analyzed
	/// </summary>
	string Content { get; set; }
}

/// <summary>
/// Developer blogs feedback service for analyzing blog comments
/// </summary>
/// <remarks>
/// This service handles retrieving and analyzing comments from developer blogs.
/// </remarks>
/// <example>
/// <code>
/// var service = new DevBlogsFeedbackService(http, config, userSettings);
/// service.ArticleUrl = "https://devblogs.microsoft.com/dotnet/announcing-net-8-preview-4/";
/// var result = await service.GetFeedback();
/// </code>
/// </example>
public interface IDevBlogsFeedbackService : IFeedbackService
{
	/// <summary>
	/// URL of the developer blog article to analyze
	/// </summary>
	string ArticleUrl { get; set; }
}
