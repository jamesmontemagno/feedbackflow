using System.Text.Json;
using SharedDump.Models.GitHub;
using SharedDump.Utils;

namespace FeedbackWebApp.Components.Feedback.Services;

public class GitHubFeedbackService : FeedbackService, IGitHubFeedbackService
{
    private readonly string _repository;
    private readonly string? _labels;
    private readonly bool _includeIssues;
    private readonly bool _includePullRequests;
    private readonly bool _includeDiscussions;

    public GitHubFeedbackService(
        HttpClient http,
        IConfiguration configuration,
        string repository,
        string? labels = null,
        bool includeIssues = true,
        bool includePullRequests = false,
        bool includeDiscussions = false,
        FeedbackStatusUpdate? onStatusUpdate = null)
        : base(http, configuration, onStatusUpdate)
    {
        _repository = repository;
        _labels = labels;
        _includeIssues = includeIssues;
        _includePullRequests = includePullRequests;
        _includeDiscussions = includeDiscussions;
    }

    public override async Task<(string markdownResult, object? additionalData)> GetFeedback()
    {
        if (string.IsNullOrWhiteSpace(_repository))
        {
            throw new InvalidOperationException("Please enter a valid GitHub repository in the format owner/repository");
        }

        UpdateStatus(FeedbackProcessStatus.GatheringComments, "Fetching GitHub feedback...");

        var githubCode = Configuration["FeedbackApi:GetGitHubFeedbackCode"] 
            ?? throw new InvalidOperationException("GitHub API code not configured");

        // Build the query parameters
        var queryParams = new List<string>
        {
            $"repo={Uri.EscapeDataString(_repository)}",
            $"issues={_includeIssues}",
            $"pulls={_includePullRequests}",
            $"discussions={_includeDiscussions}"
        };

        // Add labels if specified
        if (!string.IsNullOrWhiteSpace(_labels))
        {
            queryParams.Add($"labels={Uri.EscapeDataString(_labels)}");
        }

        // Get feedback from GitHub API
        var getFeedbackUrl = $"{BaseUrl}/api/GetGitHubFeedback?code={Uri.EscapeDataString(githubCode)}&{string.Join("&", queryParams)}";
        var feedbackResponse = await Http.GetAsync(getFeedbackUrl);
        feedbackResponse.EnsureSuccessStatusCode();

        var responseContent = await feedbackResponse.Content.ReadAsStringAsync();
        
        // Parse the JSON to extract GitHub data for potential additional display
        var githubData = JsonDocument.Parse(responseContent).RootElement;
        
        // Create a data structure for additional GitHub data display if needed
        var issues = JsonSerializer.Deserialize<List<GithubIssueModel>>(githubData.GetProperty("Issues").GetRawText());
        var pulls = JsonSerializer.Deserialize<List<GithubIssueModel>>(githubData.GetProperty("PullRequests").GetRawText());
        var discussions = JsonSerializer.Deserialize<List<GithubDiscussionModel>>(githubData.GetProperty("Discussions").GetRawText());

        // Sort and limit comments for each issue, pull, and discussion
        if (issues != null)
        {
            foreach (var issue in issues)
            {
                if (issue.Comments != null)
                    issue.Comments = issue.Comments.OrderBy(c => c.CreatedAt).Take(MaxCommentsToAnalyze).ToArray();
            }
        }
        if (pulls != null)
        {
            foreach (var pr in pulls)
            {
                if (pr.Comments != null)
                    pr.Comments = pr.Comments.OrderBy(c => c.CreatedAt).Take(MaxCommentsToAnalyze).ToArray();
            }
        }
        if (discussions != null)
        {
            foreach (var disc in discussions)
            {
                if (disc.Comments != null)
                    disc.Comments = disc.Comments.OrderBy(c => c.CreatedAt).Take(MaxCommentsToAnalyze).ToArray();
            }
        }

        var limitedJson = JsonSerializer.Serialize(new {
            Issues = issues,
            PullRequests = pulls,
            Discussions = discussions
        });

        // Analyze the comments
        UpdateStatus(FeedbackProcessStatus.AnalyzingComments, "Analyzing GitHub feedback...");
        var markdownResult = await AnalyzeComments("GitHub", limitedJson);

        // Create a data structure for additional GitHub data display if needed
        var additionalData = new
        {
            Issues = issues,
            PullRequests = pulls,
            Discussions = discussions
        };

        return (markdownResult, additionalData);
    }
}