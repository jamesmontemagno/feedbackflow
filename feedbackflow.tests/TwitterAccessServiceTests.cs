using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using FeedbackWebApp.Services;
using FeedbackWebApp.Services.Authentication;

namespace FeedbackFlow.Tests;

[TestClass]
public class TwitterAccessServiceTests
{
    private ITwitterAccessService _twitterAccessService;
    private IHttpClientFactory _httpClientFactory;
    private IConfiguration _configuration;
    private IAuthenticationHeaderService _authHeaderService;

    [TestInitialize]
    public void Initialize()
    {
        _httpClientFactory = Substitute.For<IHttpClientFactory>();
        _configuration = Substitute.For<IConfiguration>();
        _authHeaderService = Substitute.For<IAuthenticationHeaderService>();

        _twitterAccessService = new TwitterAccessService(
            _httpClientFactory,
            _configuration,
            _authHeaderService);
    }

    [TestMethod]
    [TestCategory("TwitterAccess")]
    public async Task CheckTwitterAccessAsync_WithNoTwitterUrls_ShouldAllowAccess()
    {
        // Arrange
        var urls = new[] { "https://youtube.com/watch?v=123", "https://github.com/user/repo" };

        // Act
        var (hasAccess, errorMessage) = await _twitterAccessService.CheckTwitterAccessAsync(urls);

        // Assert
        Assert.IsTrue(hasAccess);
        Assert.IsNull(errorMessage);
    }

    [TestMethod]
    [TestCategory("TwitterAccess")]
    public async Task CheckTwitterAccessAsync_WithTwitterUrls_ShouldCheckAccess()
    {
        // Arrange
        var urls = new[] { "https://twitter.com/user/status/123456789", "https://youtube.com/watch?v=123" };
        
        // Mock configuration to return null for API calls, which should result in allowing access
        _configuration["FeedbackApi:BaseUrl"].Returns((string?)null);

        // Act
        var (hasAccess, errorMessage) = await _twitterAccessService.CheckTwitterAccessAsync(urls);

        // Assert
        // Should return true when configuration is not available (fallback behavior)
        Assert.IsTrue(hasAccess);
        Assert.IsNull(errorMessage);
    }

    [TestMethod]
    [TestCategory("TwitterAccess")]
    public async Task CheckTwitterAccessAsync_WithXComUrls_ShouldCheckAccess()
    {
        // Arrange
        var urls = new[] { "https://x.com/user/status/123456789" };
        
        // Mock configuration to return null for API calls, which should result in allowing access
        _configuration["FeedbackApi:BaseUrl"].Returns((string?)null);

        // Act
        var (hasAccess, errorMessage) = await _twitterAccessService.CheckTwitterAccessAsync(urls);

        // Assert
        // Should return true when configuration is not available (fallback behavior)
        Assert.IsTrue(hasAccess);
        Assert.IsNull(errorMessage);
    }

    [TestMethod]
    [TestCategory("TwitterAccess")]
    public async Task CheckTwitterAccessAsync_WithEmptyUrls_ShouldAllowAccess()
    {
        // Arrange
        var urls = Array.Empty<string>();

        // Act
        var (hasAccess, errorMessage) = await _twitterAccessService.CheckTwitterAccessAsync(urls);

        // Assert
        Assert.IsTrue(hasAccess);
        Assert.IsNull(errorMessage);
    }

    [TestMethod]
    [TestCategory("TwitterAccess")]
    public async Task CheckTwitterAccessAsync_WithNullOrEmptyStringUrls_ShouldAllowAccess()
    {
        // Arrange
        var urls = new[] { "", null, "   " };

        // Act
        var (hasAccess, errorMessage) = await _twitterAccessService.CheckTwitterAccessAsync(urls!);

        // Assert
        Assert.IsTrue(hasAccess);
        Assert.IsNull(errorMessage);
    }
}
