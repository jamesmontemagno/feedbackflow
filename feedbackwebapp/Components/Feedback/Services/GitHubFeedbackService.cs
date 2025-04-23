using System.Text.Json;
using SharedDump.Models.GitHub;
using SharedDump.Utils;
using FeedbackWebApp.Services;

namespace FeedbackWebApp.Components.Feedback.Services;

public class GitHubFeedbackService : FeedbackService, IGitHubFeedbackService
{
    private readonly string _repository;
    private readonly string? _labels;
    private readonly bool _includeIssues;
    private readonly bool _includePullRequests;
    private readonly bool _includeDiscussions;
    private readonly string _url;

    private class GitHubResponse
    {
        public List<GithubIssueModel>? Issues { get; set; }
        public List<GithubIssueModel>? PullRequests { get; set; }
        public List<GithubDiscussionModel>? Discussions { get; set; }
    }

    public GitHubFeedbackService(
        HttpClient http,
        IConfiguration configuration,
        UserSettingsService userSettings,
        string url,
        string? labels = null,
        bool includeIssues = true,
        bool includePullRequests = false,
        bool includeDiscussions = false,
        FeedbackStatusUpdate? onStatusUpdate = null)
        : base(http, configuration, userSettings, onStatusUpdate)
    {
        _url = url;
        _repository = "";  // Will be parsed from URL
        _labels = labels;
        _includeIssues = includeIssues;
        _includePullRequests = includePullRequests;
        _includeDiscussions = includeDiscussions;
    }

    public override async Task<(string markdownResult, object? additionalData)> GetFeedback()
    {
        var maxComments = await GetMaxCommentsToAnalyze();

        var parseResult = GitHubUrlParser.ParseUrl(_url);
        if (parseResult == null)
        {
            throw new InvalidOperationException("Please enter a valid GitHub URL");
        }

        var (owner, repo, type, number) = parseResult.Value;

        UpdateStatus(FeedbackProcessStatus.GatheringComments, "Fetching GitHub comments...");

        var githubCode = Configuration["FeedbackApi:GetGitHubFeedbackCode"]
            ?? throw new InvalidOperationException("GitHub API code not configured");

        // Get comments from the GitHub API
        var queryParams = new List<string>
        {
            $"code={Uri.EscapeDataString(githubCode)}",
            $"maxComments={maxComments}"
        };

        if (!string.IsNullOrEmpty(owner) && !string.IsNullOrEmpty(repo))
        {
            queryParams.Add($"repo={Uri.EscapeDataString($"{owner}/{repo}")}");
        }

        var getFeedbackUrl = $"{BaseUrl}/api/GetGitHubFeedback?{string.Join("&", queryParams)}";
        var feedbackResponse = await Http.GetAsync(getFeedbackUrl);
        feedbackResponse.EnsureSuccessStatusCode();
        var responseContent = await feedbackResponse.Content.ReadAsStringAsync();

        var response = JsonSerializer.Deserialize<GitHubResponse>(responseContent);

        var issues = response?.Issues;
        var pulls = response?.PullRequests;
        var discussions = response?.Discussions;

        if ((issues == null || !issues.Any()) && 
            (pulls == null || !pulls.Any()) && 
            (discussions == null || !discussions.Any()))
        {
            throw new InvalidOperationException("No comments found in the specified GitHub items");
        }

        // Calculate total comments across all items
        var totalIssueComments = issues?.Sum(i => i.Comments?.Length ?? 0) ?? 0;
        var totalPRComments = pulls?.Sum(p => p.Comments?.Length ?? 0) ?? 0;
        var totalDiscussionComments = discussions?.Sum(d => d.Comments?.Length ?? 0) ?? 0;
        var totalComments = totalIssueComments + totalPRComments + totalDiscussionComments;
        var totalItems = (issues?.Count ?? 0) + (pulls?.Count ?? 0) + (discussions?.Count ?? 0);

        UpdateStatus(FeedbackProcessStatus.GatheringComments, 
            $"Found {totalComments} comments across {totalItems} items ({issues?.Count ?? 0} issues, {pulls?.Count ?? 0} PRs, {discussions?.Count ?? 0} discussions)...");

        // Sort and limit comments for each item type
        if (issues != null)
        {
            foreach (var issue in issues)
            {
                if (issue.Comments != null)
                    issue.Comments = issue.Comments.OrderBy(c => c.CreatedAt).Take(maxComments).ToArray();
            }
        }
        if (pulls != null)
        {
            foreach (var pr in pulls)
            {
                if (pr.Comments != null)
                    pr.Comments = pr.Comments.OrderBy(c => c.CreatedAt).Take(maxComments).ToArray();
            }
        }
        if (discussions != null)
        {
            foreach (var disc in discussions)
            {
                if (disc.Comments != null)
                    disc.Comments = disc.Comments.OrderBy(c => c.CreatedAt).Take(maxComments).ToArray();
            }
        }

        var limitedJson = JsonSerializer.Serialize(new {
            Issues = issues,
            PullRequests = pulls,
            Discussions = discussions
        });

        // Analyze the comments
        var markdownResult = await AnalyzeComments("GitHub", limitedJson);

        // Create a data structure for additional GitHub data display
        var additionalData = new
        {
            Issues = issues,
            PullRequests = pulls,
            Discussions = discussions
        };

        return (markdownResult, additionalData);
    }
}