using System.Text.Json;
using SharedDump.Models.GitHub;
using SharedDump.Utils;
using FeedbackWebApp.Components.Feedback;
using FeedbackWebApp.Services.Interfaces;

namespace FeedbackWebApp.Services.Feedback;

public class GitHubFeedbackService : FeedbackService, IGitHubFeedbackService
{
    private readonly string _url;

    public GitHubFeedbackService(
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

        var responseContent = await feedbackResponse.Content.ReadAsStringAsync();        var comments = JsonSerializer.Deserialize<List<GithubCommentModel>>(responseContent);

        if (comments == null || !comments.Any())
        {
            throw new InvalidOperationException("No comments found for the specified item");
        }

        var totalComments = comments.Count;
        UpdateStatus(FeedbackProcessStatus.AnalyzingComments, "Analyzing comments...");

        // Analyze the comments and return both the markdown result and the comments list
        var markdown = await AnalyzeComments("github", responseContent, totalComments);
        return (markdown, comments);
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