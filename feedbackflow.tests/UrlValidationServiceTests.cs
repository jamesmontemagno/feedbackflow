using SharedDump.Services;

namespace FeedbackFlow.Tests;

[TestClass]
public class UrlValidationServiceTests
{
    [TestClass]
    public class GitHubOwnerNameValidationTests
    {
        [TestMethod]
        public void ValidateGitHubOwnerName_ValidOwnerName_ReturnsValid()
        {
            var result = UrlValidationService.ValidateGitHubOwnerName("microsoft");
            
            Assert.IsTrue(result.IsValid);
            Assert.IsNull(result.ErrorMessage);
        }

        [TestMethod]
        public void ValidateGitHubOwnerName_ValidOwnerNameWithHyphens_ReturnsValid()
        {
            var result = UrlValidationService.ValidateGitHubOwnerName("test-user");
            
            Assert.IsTrue(result.IsValid);
            Assert.IsNull(result.ErrorMessage);
        }

        [TestMethod]
        public void ValidateGitHubOwnerName_ValidOwnerNameWithNumbers_ReturnsValid()
        {
            var result = UrlValidationService.ValidateGitHubOwnerName("user123");
            
            Assert.IsTrue(result.IsValid);
            Assert.IsNull(result.ErrorMessage);
        }

        [TestMethod]
        public void ValidateGitHubOwnerName_SingleCharacter_ReturnsValid()
        {
            var result = UrlValidationService.ValidateGitHubOwnerName("a");
            
            Assert.IsTrue(result.IsValid);
            Assert.IsNull(result.ErrorMessage);
        }

        [TestMethod]
        public void ValidateGitHubOwnerName_EmptyName_ReturnsError()
        {
            var result = UrlValidationService.ValidateGitHubOwnerName("");
            
            Assert.IsFalse(result.IsValid);
            Assert.AreEqual("GitHub owner name is required.", result.ErrorMessage);
        }

        [TestMethod]
        public void ValidateGitHubOwnerName_NullName_ReturnsError()
        {
            var result = UrlValidationService.ValidateGitHubOwnerName(null);
            
            Assert.IsFalse(result.IsValid);
            Assert.AreEqual("GitHub owner name is required.", result.ErrorMessage);
        }

        [TestMethod]
        public void ValidateGitHubOwnerName_TooLong_ReturnsError()
        {
            var longName = new string('a', 40); // 40 characters, exceeds GitHub's 39 limit
            var result = UrlValidationService.ValidateGitHubOwnerName(longName);
            
            Assert.IsFalse(result.IsValid);
            Assert.AreEqual("GitHub owner name cannot be longer than 39 characters.", result.ErrorMessage);
        }

        [TestMethod]
        public void ValidateGitHubOwnerName_StartsWithHyphen_ReturnsError()
        {
            var result = UrlValidationService.ValidateGitHubOwnerName("-invalid");
            
            Assert.IsFalse(result.IsValid);
            Assert.AreEqual("GitHub owner name can only contain alphanumeric characters and hyphens, and cannot start or end with a hyphen.", result.ErrorMessage);
        }

        [TestMethod]
        public void ValidateGitHubOwnerName_EndsWithHyphen_ReturnsError()
        {
            var result = UrlValidationService.ValidateGitHubOwnerName("invalid-");
            
            Assert.IsFalse(result.IsValid);
            Assert.AreEqual("GitHub owner name can only contain alphanumeric characters and hyphens, and cannot start or end with a hyphen.", result.ErrorMessage);
        }

        [TestMethod]
        public void ValidateGitHubOwnerName_ConsecutiveHyphens_ReturnsError()
        {
            var result = UrlValidationService.ValidateGitHubOwnerName("test--user");
            
            Assert.IsFalse(result.IsValid);
            Assert.AreEqual("GitHub owner name cannot contain consecutive hyphens.", result.ErrorMessage);
        }

        [TestMethod]
        public void ValidateGitHubOwnerName_InvalidCharacters_ReturnsError()
        {
            var result = UrlValidationService.ValidateGitHubOwnerName("user@domain");
            
            Assert.IsFalse(result.IsValid);
            Assert.AreEqual("GitHub owner name can only contain alphanumeric characters and hyphens, and cannot start or end with a hyphen.", result.ErrorMessage);
        }
    }

    [TestClass]
    public class GitHubRepoNameValidationTests
    {
        [TestMethod]
        public void ValidateGitHubRepoName_ValidRepoName_ReturnsValid()
        {
            var result = UrlValidationService.ValidateGitHubRepoName("vscode");
            
            Assert.IsTrue(result.IsValid);
            Assert.IsNull(result.ErrorMessage);
        }

        [TestMethod]
        public void ValidateGitHubRepoName_ValidRepoNameWithHyphens_ReturnsValid()
        {
            var result = UrlValidationService.ValidateGitHubRepoName("my-repo");
            
            Assert.IsTrue(result.IsValid);
            Assert.IsNull(result.ErrorMessage);
        }

        [TestMethod]
        public void ValidateGitHubRepoName_ValidRepoNameWithUnderscores_ReturnsValid()
        {
            var result = UrlValidationService.ValidateGitHubRepoName("my_repo");
            
            Assert.IsTrue(result.IsValid);
            Assert.IsNull(result.ErrorMessage);
        }

        [TestMethod]
        public void ValidateGitHubRepoName_ValidRepoNameWithDots_ReturnsValid()
        {
            var result = UrlValidationService.ValidateGitHubRepoName("my.repo");
            
            Assert.IsTrue(result.IsValid);
            Assert.IsNull(result.ErrorMessage);
        }

        [TestMethod]
        public void ValidateGitHubRepoName_EmptyName_ReturnsError()
        {
            var result = UrlValidationService.ValidateGitHubRepoName("");
            
            Assert.IsFalse(result.IsValid);
            Assert.AreEqual("GitHub repository name is required.", result.ErrorMessage);
        }

        [TestMethod]
        public void ValidateGitHubRepoName_NullName_ReturnsError()
        {
            var result = UrlValidationService.ValidateGitHubRepoName(null);
            
            Assert.IsFalse(result.IsValid);
            Assert.AreEqual("GitHub repository name is required.", result.ErrorMessage);
        }

        [TestMethod]
        public void ValidateGitHubRepoName_TooLong_ReturnsError()
        {
            var longName = new string('a', 101); // 101 characters, exceeds GitHub's 100 limit
            var result = UrlValidationService.ValidateGitHubRepoName(longName);
            
            Assert.IsFalse(result.IsValid);
            Assert.AreEqual("GitHub repository name cannot be longer than 100 characters.", result.ErrorMessage);
        }

        [TestMethod]
        public void ValidateGitHubRepoName_StartsWithDot_ReturnsError()
        {
            var result = UrlValidationService.ValidateGitHubRepoName(".invalid");
            
            Assert.IsFalse(result.IsValid);
            Assert.AreEqual("GitHub repository name cannot start with a period.", result.ErrorMessage);
        }

        [TestMethod]
        public void ValidateGitHubRepoName_InvalidCharacters_ReturnsError()
        {
            var result = UrlValidationService.ValidateGitHubRepoName("repo@name");
            
            Assert.IsFalse(result.IsValid);
            Assert.AreEqual("GitHub repository name can only contain letters, numbers, hyphens, underscores, and periods.", result.ErrorMessage);
        }
    }

    [TestClass]
    public class SubredditNameValidationTests
    {
        [TestMethod]
        public void ValidateSubredditName_ValidSubredditName_ReturnsValid()
        {
            var result = UrlValidationService.ValidateSubredditName("dotnet");
            
            Assert.IsTrue(result.IsValid);
            Assert.IsNull(result.ErrorMessage);
        }

        [TestMethod]
        public void ValidateSubredditName_ValidSubredditNameWithNumbers_ReturnsValid()
        {
            var result = UrlValidationService.ValidateSubredditName("test123");
            
            Assert.IsTrue(result.IsValid);
            Assert.IsNull(result.ErrorMessage);
        }

        [TestMethod]
        public void ValidateSubredditName_ValidSubredditNameWithUnderscores_ReturnsValid()
        {
            var result = UrlValidationService.ValidateSubredditName("test_subreddit");
            
            Assert.IsTrue(result.IsValid);
            Assert.IsNull(result.ErrorMessage);
        }

        [TestMethod]
        public void ValidateSubredditName_ValidSubredditNameWithRPrefix_ReturnsValid()
        {
            var result = UrlValidationService.ValidateSubredditName("r/dotnet");
            
            Assert.IsTrue(result.IsValid);
            Assert.IsNull(result.ErrorMessage);
        }

        [TestMethod]
        public void ValidateSubredditName_EmptyName_ReturnsError()
        {
            var result = UrlValidationService.ValidateSubredditName("");
            
            Assert.IsFalse(result.IsValid);
            Assert.AreEqual("Subreddit name is required.", result.ErrorMessage);
        }

        [TestMethod]
        public void ValidateSubredditName_NullName_ReturnsError()
        {
            var result = UrlValidationService.ValidateSubredditName(null);
            
            Assert.IsFalse(result.IsValid);
            Assert.AreEqual("Subreddit name is required.", result.ErrorMessage);
        }

        [TestMethod]
        public void ValidateSubredditName_TooShort_ReturnsError()
        {
            var result = UrlValidationService.ValidateSubredditName("ab");
            
            Assert.IsFalse(result.IsValid);
            Assert.AreEqual("Subreddit name must be 3-21 characters and contain only letters, numbers, and underscores.", result.ErrorMessage);
        }

        [TestMethod]
        public void ValidateSubredditName_TooLong_ReturnsError()
        {
            var longName = new string('a', 22); // 22 characters, exceeds Reddit's 21 limit
            var result = UrlValidationService.ValidateSubredditName(longName);
            
            Assert.IsFalse(result.IsValid);
            Assert.AreEqual("Subreddit name must be 3-21 characters and contain only letters, numbers, and underscores.", result.ErrorMessage);
        }

        [TestMethod]
        public void ValidateSubredditName_InvalidCharacters_ReturnsError()
        {
            var result = UrlValidationService.ValidateSubredditName("test-subreddit");
            
            Assert.IsFalse(result.IsValid);
            Assert.AreEqual("Subreddit name must be 3-21 characters and contain only letters, numbers, and underscores.", result.ErrorMessage);
        }

        [TestMethod]
        public void ValidateSubredditName_ReservedName_ReturnsError()
        {
            var result = UrlValidationService.ValidateSubredditName("admin");
            
            Assert.IsFalse(result.IsValid);
            Assert.AreEqual("This subreddit name is reserved and not allowed.", result.ErrorMessage);
        }
    }

    [TestClass]
    public class UrlConstructionTests
    {
        [TestMethod]
        public void ConstructGitHubUrl_ValidInputs_ReturnsCorrectUrl()
        {
            var url = UrlValidationService.ConstructGitHubUrl("microsoft", "vscode");
            
            Assert.AreEqual("https://github.com/microsoft/vscode", url);
        }

        [TestMethod]
        public void ConstructRedditUrl_ValidInput_ReturnsCorrectUrl()
        {
            var url = UrlValidationService.ConstructRedditUrl("dotnet");
            
            Assert.AreEqual("https://www.reddit.com/r/dotnet/", url);
        }
    }
}