using SharedDump.Services;

namespace FeedbackFlow.Tests;

[TestClass]
public class UrlValidationServiceTests
{
    [TestClass]
    public class GitHubUrlValidationTests
    {
        [TestMethod]
        public void ValidateGitHubUrl_ValidIssueUrl_ReturnsValid()
        {
            var url = "https://github.com/microsoft/vscode/issues/123";
            var result = UrlValidationService.ValidateGitHubUrl(url);
            
            Assert.IsTrue(result.IsValid);
            Assert.IsNull(result.ErrorMessage);
            Assert.IsNotNull(result.ParsedData);
            
            var data = (GitHubUrlData)result.ParsedData;
            Assert.AreEqual("microsoft", data.Owner);
            Assert.AreEqual("vscode", data.Repository);
            Assert.AreEqual("issue", data.Type);
            Assert.AreEqual(123, data.Number);
        }

        [TestMethod]
        public void ValidateGitHubUrl_ValidPullRequestUrl_ReturnsValid()
        {
            var url = "https://github.com/dotnet/aspnetcore/pull/456";
            var result = UrlValidationService.ValidateGitHubUrl(url);
            
            Assert.IsTrue(result.IsValid);
            Assert.IsNull(result.ErrorMessage);
            Assert.IsNotNull(result.ParsedData);
            
            var data = (GitHubUrlData)result.ParsedData;
            Assert.AreEqual("dotnet", data.Owner);
            Assert.AreEqual("aspnetcore", data.Repository);
            Assert.AreEqual("pull", data.Type);
            Assert.AreEqual(456, data.Number);
        }

        [TestMethod]
        public void ValidateGitHubUrl_ValidDiscussionUrl_ReturnsValid()
        {
            var url = "https://github.com/github/docs/discussions/789";
            var result = UrlValidationService.ValidateGitHubUrl(url);
            
            Assert.IsTrue(result.IsValid);
            Assert.IsNull(result.ErrorMessage);
            Assert.IsNotNull(result.ParsedData);
            
            var data = (GitHubUrlData)result.ParsedData;
            Assert.AreEqual("github", data.Owner);
            Assert.AreEqual("docs", data.Repository);
            Assert.AreEqual("discussion", data.Type);
            Assert.AreEqual(789, data.Number);
        }

        [TestMethod]
        public void ValidateGitHubUrl_EmptyUrl_ReturnsError()
        {
            var result = UrlValidationService.ValidateGitHubUrl("");
            
            Assert.IsFalse(result.IsValid);
            Assert.AreEqual("GitHub URL is required.", result.ErrorMessage);
            Assert.IsNull(result.ParsedData);
        }

        [TestMethod]
        public void ValidateGitHubUrl_NullUrl_ReturnsError()
        {
            var result = UrlValidationService.ValidateGitHubUrl(null);
            
            Assert.IsFalse(result.IsValid);
            Assert.AreEqual("GitHub URL is required.", result.ErrorMessage);
            Assert.IsNull(result.ParsedData);
        }

        [TestMethod]
        public void ValidateGitHubUrl_InvalidUrl_ReturnsError()
        {
            var result = UrlValidationService.ValidateGitHubUrl("not-a-url");
            
            Assert.IsFalse(result.IsValid);
            Assert.AreEqual("Please enter a valid GitHub URL.", result.ErrorMessage);
            Assert.IsNull(result.ParsedData);
        }

        [TestMethod]
        public void ValidateGitHubUrl_NonGitHubUrl_ReturnsError()
        {
            var url = "https://www.google.com";
            var result = UrlValidationService.ValidateGitHubUrl(url);
            
            Assert.IsFalse(result.IsValid);
            Assert.AreEqual("Please enter a valid GitHub URL.", result.ErrorMessage);
            Assert.IsNull(result.ParsedData);
        }

        [TestMethod]
        public void ValidateGitHubUrl_UnsupportedGitHubUrl_ReturnsError()
        {
            var url = "https://github.com/microsoft/vscode/releases";
            var result = UrlValidationService.ValidateGitHubUrl(url);
            
            Assert.IsFalse(result.IsValid);
            Assert.AreEqual("GitHub URL must be a repository, issue, pull request, or discussion URL (e.g., https://github.com/owner/repo/issues/123).", result.ErrorMessage);
            Assert.IsNull(result.ParsedData);
        }

        [TestMethod]
        public void ValidateGitHubUrl_RepoRootUrl_ReturnsError()
        {
            var url = "https://github.com/microsoft/vscode";
            var result = UrlValidationService.ValidateGitHubUrl(url);
            
            Assert.IsFalse(result.IsValid);
            Assert.AreEqual("GitHub URL must be a repository, issue, pull request, or discussion URL (e.g., https://github.com/owner/repo/issues/123).", result.ErrorMessage);
            Assert.IsNull(result.ParsedData);
        }
    }

    [TestClass]
    public class RedditUrlValidationTests
    {
        [TestMethod]
        public void ValidateRedditUrl_ValidPostUrl_ReturnsValid()
        {
            var url = "https://www.reddit.com/r/dotnet/comments/abc123/some-title/";
            var result = UrlValidationService.ValidateRedditUrl(url);
            
            Assert.IsTrue(result.IsValid);
            Assert.IsNull(result.ErrorMessage);
            Assert.IsNotNull(result.ParsedData);
            
            var data = (RedditUrlData)result.ParsedData;
            Assert.AreEqual("dotnet", data.Subreddit);
            Assert.AreEqual("abc123", data.ThreadId);
        }

        [TestMethod]
        public void ValidateRedditUrl_ValidPostUrlWithoutTrailingSlash_ReturnsValid()
        {
            var url = "https://www.reddit.com/r/programming/comments/xyz789/another-title";
            var result = UrlValidationService.ValidateRedditUrl(url);
            
            Assert.IsTrue(result.IsValid);
            Assert.IsNull(result.ErrorMessage);
            Assert.IsNotNull(result.ParsedData);
            
            var data = (RedditUrlData)result.ParsedData;
            Assert.AreEqual("programming", data.Subreddit);
            Assert.AreEqual("xyz789", data.ThreadId);
        }

        [TestMethod]
        public void ValidateRedditUrl_EmptyUrl_ReturnsError()
        {
            var result = UrlValidationService.ValidateRedditUrl("");
            
            Assert.IsFalse(result.IsValid);
            Assert.AreEqual("Reddit URL is required.", result.ErrorMessage);
            Assert.IsNull(result.ParsedData);
        }

        [TestMethod]
        public void ValidateRedditUrl_NullUrl_ReturnsError()
        {
            var result = UrlValidationService.ValidateRedditUrl(null);
            
            Assert.IsFalse(result.IsValid);
            Assert.AreEqual("Reddit URL is required.", result.ErrorMessage);
            Assert.IsNull(result.ParsedData);
        }

        [TestMethod]
        public void ValidateRedditUrl_InvalidUrl_ReturnsError()
        {
            var result = UrlValidationService.ValidateRedditUrl("not-a-url");
            
            Assert.IsFalse(result.IsValid);
            Assert.AreEqual("Please enter a valid Reddit URL.", result.ErrorMessage);
            Assert.IsNull(result.ParsedData);
        }

        [TestMethod]
        public void ValidateRedditUrl_NonRedditUrl_ReturnsError()
        {
            var url = "https://www.google.com";
            var result = UrlValidationService.ValidateRedditUrl(url);
            
            Assert.IsFalse(result.IsValid);
            Assert.AreEqual("Please enter a valid Reddit URL.", result.ErrorMessage);
            Assert.IsNull(result.ParsedData);
        }

        [TestMethod]
        public void ValidateRedditUrl_UnsupportedRedditUrl_ReturnsError()
        {
            var url = "https://www.reddit.com/r/dotnet/hot/";
            var result = UrlValidationService.ValidateRedditUrl(url);
            
            Assert.IsFalse(result.IsValid);
            Assert.AreEqual("Reddit URL must be a valid post or comment URL (e.g., https://www.reddit.com/r/subreddit/comments/abc123/title/).", result.ErrorMessage);
            Assert.IsNull(result.ParsedData);
        }

        [TestMethod]
        public void ValidateRedditUrl_SubredditRootUrl_ReturnsError()
        {
            var url = "https://www.reddit.com/r/dotnet/";
            var result = UrlValidationService.ValidateRedditUrl(url);
            
            Assert.IsFalse(result.IsValid);
            Assert.AreEqual("Reddit URL must be a valid post or comment URL (e.g., https://www.reddit.com/r/subreddit/comments/abc123/title/).", result.ErrorMessage);
            Assert.IsNull(result.ParsedData);
        }

        [TestMethod]
        public void ValidateRedditUrl_RedditUrlWithoutSubreddit_ReturnsError()
        {
            var url = "https://www.reddit.com/comments/abc123/";
            var result = UrlValidationService.ValidateRedditUrl(url);
            
            Assert.IsFalse(result.IsValid);
            Assert.AreEqual("Reddit URL must be a valid post or comment URL (e.g., https://www.reddit.com/r/subreddit/comments/abc123/title/).", result.ErrorMessage);
            Assert.IsNull(result.ParsedData);
        }
    }
}