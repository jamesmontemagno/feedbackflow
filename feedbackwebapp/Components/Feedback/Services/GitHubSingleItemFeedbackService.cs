using System.Text.Json;
using SharedDump.Models.GitHub;
using SharedDump.Utils;
using FeedbackWebApp.Services;

namespace FeedbackWebApp.Components.Feedback.Services;

public class GitHubSingleItemFeedbackService : FeedbackService, IGitHubFeedbackService
{
    private readonly string _url;

    public GitHubSingleItemFeedbackService(
        HttpClient http,
        IConfiguration configuration,
        UserSettingsService userSettings,
        string url,
        FeedbackStatusUpdate? onStatusUpdate = null)
        : base(http, configuration, userSettings, onStatusUpdate)
    {
        _url = url;
    }

    public override async Task<(string markdownResult, object? additionalData)> GetFeedback()
    {
        var parsedUrl = GitHubUrlParser.ParseUrl(_url);
        if (parsedUrl == null)
        {
            throw new InvalidOperationException("Please enter a valid GitHub issue, pull request, or discussion URL");
        }

        UpdateStatus(FeedbackProcessStatus.GatheringComments, "Fetching GitHub comments...");

        var githubCode = Configuration["FeedbackApi:GetGitHubFeedbackCode"]
            ?? throw new InvalidOperationException("GitHub API code not configured");

        var maxComments = await GetMaxCommentsToAnalyze();

        // Get comments from the GitHub API
        var getFeedbackUrl = $"{BaseUrl}/api/GetGitHubFeedback?code={Uri.EscapeDataString(githubCode)}&url={Uri.EscapeDataString(_url)}&maxComments={maxComments}";
        var feedbackResponse = await Http.GetAsync(getFeedbackUrl);
        feedbackResponse.EnsureSuccessStatusCode();
        var responseContent = await feedbackResponse.Content.ReadAsStringAsync();

        // Handle the response based on the item type
        var content = parsedUrl.Value.type switch
        {
            "issue" or "pull" => HandleIssueContent(responseContent),
            "discussion" => HandleDiscussionContent(responseContent),
            _ => throw new InvalidOperationException("Unsupported GitHub URL type")
        };

        // Analyze the comments
        var markdownResult = await AnalyzeComments("github", content);

        return (markdownResult, content);
    }

    private static string HandleIssueContent(string responseContent)
    {
        var issue = JsonSerializer.Deserialize<GithubIssueModel>(responseContent)
            ?? throw new InvalidOperationException("Failed to parse GitHub issue/PR data");

        return $"{(issue.URL.Contains("/pull/") ? "Pull Request" : "Issue")} #{issue.Id}: {issue.Title}\n" +
               $"Description: {issue.Body}\n" +
               string.Join("\n", issue.Comments.Select(c => $"Comment by {c.Author}: {c.Content}"));
    }

    private static string HandleDiscussionContent(string responseContent)
    {
        var discussion = JsonSerializer.Deserialize<GithubDiscussionModel>(responseContent)
            ?? throw new InvalidOperationException("Failed to parse GitHub discussion data");

        return $"Discussion: {discussion.Title}\n" +
               string.Join("\n", discussion.Comments.Select(c => $"Comment by {c.Author}: {c.Content}"));
    }
}