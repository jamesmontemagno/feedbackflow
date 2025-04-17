using System.Text.Json;
using SharedDump.Models.GitHub;

namespace FeedbackWebApp.Components.Feedback.Services;

public class GitHubSingleItemFeedbackService : FeedbackService, IGitHubFeedbackService
{
    private readonly string _url;

    public GitHubSingleItemFeedbackService(
        HttpClient http,
        IConfiguration configuration,
        string url,
        FeedbackStatusUpdate? onStatusUpdate = null)
        : base(http, configuration, onStatusUpdate)
    {
        _url = url;
    }

    public override async Task<(string markdownResult, object? additionalData)> GetFeedback()
    {
        UpdateStatus(FeedbackProcessStatus.GatheringComments, "Parsing GitHub URL...");

        var parseResult = GitHubUrlParser.ParseUrl(_url);
        if (parseResult == null)
        {
            throw new InvalidOperationException("Invalid GitHub URL. Please provide a valid issue, pull request, or discussion URL.");
        }

        var (owner, repo, type, number) = parseResult.Value;

        UpdateStatus(FeedbackProcessStatus.GatheringComments, "Fetching GitHub comments...");

        var getFeedbackCode = Configuration["FeedbackApi:GetGitHubFeedbackCode"]
            ?? throw new InvalidOperationException("GitHub API code not configured");

        var queryParams = new List<string>
        {
            $"code={Uri.EscapeDataString(getFeedbackCode)}",
            $"repo={Uri.EscapeDataString($"{owner}/{repo}")}",
            $"type={Uri.EscapeDataString(type)}",
            $"number={number}"
        };

        var getFeedbackUrl = $"{BaseUrl}/api/GetGitHubItemComments?{string.Join("&", queryParams)}";
        var feedbackResponse = await Http.GetAsync(getFeedbackUrl);
        feedbackResponse.EnsureSuccessStatusCode();

        var responseContent = await feedbackResponse.Content.ReadAsStringAsync();
        var comments = JsonSerializer.Deserialize<List<GithubCommentModel>>(responseContent);

        if (comments == null || !comments.Any())
        {
            throw new InvalidOperationException("No comments found for the specified item");
        }

        UpdateStatus(FeedbackProcessStatus.AnalyzingComments, "Analyzing comments...");

        // Analyze the comments and return both the markdown result and the comments list
        var markdown = await AnalyzeComments("github", responseContent);
        return (markdown, comments);
    }
}