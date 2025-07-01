using SharedDump.Models.GitHub;

namespace SharedDump.Services.Interfaces;

public interface IGitHubService
{
    Task<bool> CheckRepositoryValid(string repoOwner, string repoName);
    Task<List<GithubDiscussionModel>> GetDiscussionsAsync(string repoOwner, string repoName);
    Task<List<GithubIssueModel>> GetIssuesAsync(string repoOwner, string repoName, string[] labels);
    Task<List<GithubIssueModel>> GetPullRequestsAsync(string repoOwner, string repoName, string[] labels);
    Task<GithubIssueModel?> GetIssueWithCommentsAsync(string repoOwner, string repoName, int issueNumber);
    Task<GithubIssueModel?> GetPullRequestWithCommentsAsync(string repoOwner, string repoName, int pullNumber);
    Task<GithubDiscussionModel?> GetDiscussionWithCommentsAsync(string repoOwner, string repoName, int discussionNumber);
    Task<List<GithubIssueSummary>> GetRecentIssuesForReportAsync(string repoOwner, string repoName, int daysBack = 7);
    Task<List<GithubIssueSummary>> GetOldestImportantIssuesWithRecentActivityAsync(string repoOwner, string repoName, int recentDays = 7, int topCount = 3);
}
