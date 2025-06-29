using SharedDump.Models.GitHub;

namespace SharedDump.Services.Interfaces;

public interface IGitHubService
{
    Task<bool> CheckRepositoryValid(string repoOwner, string repoName);
    Task<List<GithubDiscussionModel>> GetDiscussionsAsync(string repoOwner, string repoName);
    Task<List<GithubIssueModel>> GetIssuesAsync(string repoOwner, string repoName, string[] labels);
    Task<List<GithubIssueModel>> GetPullRequestsAsync(string repoOwner, string repoName, string[] labels);
    Task<List<GithubCommentModel>> GetIssueCommentsAsync(string repoOwner, string repoName, int issueNumber);
    Task<List<GithubCommentModel>> GetPullRequestCommentsAsync(string repoOwner, string repoName, int pullNumber);
    Task<List<GithubCommentModel>> GetDiscussionCommentsAsync(string repoOwner, string repoName, int discussionNumber);
    Task<List<GithubIssueSummary>> GetRecentIssuesForReportAsync(string repoOwner, string repoName, int daysBack = 7);
    Task<List<GithubIssueSummary>> GetOldestImportantIssuesWithRecentActivityAsync(string repoOwner, string repoName, int recentDays = 7, int topCount = 3);
}
