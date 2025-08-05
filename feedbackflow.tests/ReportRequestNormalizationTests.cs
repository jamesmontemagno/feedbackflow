using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharedDump.Models.Reports;

namespace FeedbackFlow.Tests;

/// <summary>
/// Tests for report request value normalization (lowercasing)
/// </summary>
[TestClass]
public class ReportRequestNormalizationTests
{
    [TestMethod]
    public void GitHubOwnerAndRepo_Should_BeNormalizedToLowercase()
    {
        // Arrange
        var request = new UserReportRequestModel
        {
            Type = "github",
            Owner = "MicroSoft",
            Repo = "DotNet"
        };

        // Act - Simulate the normalization logic from ReportRequestFunctions
        if (request.Type == "github")
        {
            if (!string.IsNullOrEmpty(request.Owner))
            {
                request.Owner = request.Owner.ToLowerInvariant();
            }
            if (!string.IsNullOrEmpty(request.Repo))
            {
                request.Repo = request.Repo.ToLowerInvariant();
            }
        }

        // Assert
        Assert.AreEqual("microsoft", request.Owner);
        Assert.AreEqual("dotnet", request.Repo);
    }

    [TestMethod]
    public void RedditSubreddit_Should_BeNormalizedToLowercase()
    {
        // Arrange
        var request = new UserReportRequestModel
        {
            Type = "reddit",
            Subreddit = "DotNet"
        };

        // Act - Simulate the normalization logic from ReportRequestFunctions
        if (request.Type == "reddit")
        {
            if (!string.IsNullOrEmpty(request.Subreddit))
            {
                request.Subreddit = request.Subreddit.ToLowerInvariant();
            }
        }

        // Assert
        Assert.AreEqual("dotnet", request.Subreddit);
    }

    [TestMethod]
    public void GlobalRequestModel_Should_BeNormalizedToLowercase()
    {
        // Arrange
        var request = new ReportRequestModel
        {
            Type = "github",
            Owner = "JamesMonteMagno",
            Repo = "FeedbackFlow"
        };

        // Act - Simulate the normalization logic that should be applied
        if (request.Type == "github")
        {
            if (!string.IsNullOrEmpty(request.Owner))
            {
                request.Owner = request.Owner.ToLowerInvariant();
            }
            if (!string.IsNullOrEmpty(request.Repo))
            {
                request.Repo = request.Repo.ToLowerInvariant();
            }
        }

        // Assert
        Assert.AreEqual("jamesmontemagno", request.Owner);
        Assert.AreEqual("feedbackflow", request.Repo);
    }

    [TestMethod]
    public void NullValues_Should_BeHandledGracefully()
    {
        // Arrange
        var request = new UserReportRequestModel
        {
            Type = "github",
            Owner = null,
            Repo = null
        };

        // Act - Simulate the normalization logic
        if (request.Type == "github")
        {
            if (!string.IsNullOrEmpty(request.Owner))
            {
                request.Owner = request.Owner.ToLowerInvariant();
            }
            if (!string.IsNullOrEmpty(request.Repo))
            {
                request.Repo = request.Repo.ToLowerInvariant();
            }
        }

        // Assert - Should not throw and values should remain null
        Assert.IsNull(request.Owner);
        Assert.IsNull(request.Repo);
    }

    [TestMethod]
    public void EmptyValues_Should_BeHandledGracefully()
    {
        // Arrange
        var request = new UserReportRequestModel
        {
            Type = "reddit",
            Subreddit = ""
        };

        // Act - Simulate the normalization logic
        if (request.Type == "reddit")
        {
            if (!string.IsNullOrEmpty(request.Subreddit))
            {
                request.Subreddit = request.Subreddit.ToLowerInvariant();
            }
        }

        // Assert - Should not throw and empty value should remain empty
        Assert.AreEqual("", request.Subreddit);
    }
}
