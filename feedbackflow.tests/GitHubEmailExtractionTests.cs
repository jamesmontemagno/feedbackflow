using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharedDump.Models.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using FeedbackWebApp.Services.Authentication;
using FeedbackWebApp.Services;
using NSubstitute;

namespace FeedbackFlow.Tests;

[TestClass]
public class GitHubEmailExtractionTests
{
    [TestMethod]
    public void GetEmailFromClaims_WithStandardEmailClaim_ReturnsEmail()
    {
        // Arrange
        var clientPrincipal = new ClientPrincipal
        {
            IdentityProvider = "github",
            Claims = new[]
            {
                new ClientPrincipalClaim { Type = "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress", Value = "user@example.com" }
            }
        };

        // Act
        var email = clientPrincipal.GetEmailFromClaims("github");

        // Assert
        Assert.AreEqual("user@example.com", email);
    }

    [TestMethod]
    public void GetEmailFromClaims_WithGitHubSpecificClaim_ReturnsEmail()
    {
        // Arrange
        var clientPrincipal = new ClientPrincipal
        {
            IdentityProvider = "github",
            Claims = new[]
            {
                new ClientPrincipalClaim { Type = "urn:github:email", Value = "github-user@example.com" }
            }
        };

        // Act
        var email = clientPrincipal.GetEmailFromClaims("github");

        // Assert
        Assert.AreEqual("github-user@example.com", email);
    }

    [TestMethod]
    public void GetEmailFromClaims_WithMultipleClaims_PrioritizesGitHubSpecific()
    {
        // Arrange
        var clientPrincipal = new ClientPrincipal
        {
            IdentityProvider = "github",
            Claims = new[]
            {
                new ClientPrincipalClaim { Type = "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress", Value = "standard@example.com" },
                new ClientPrincipalClaim { Type = "urn:github:primary_email", Value = "github-primary@example.com" }
            }
        };

        // Act
        var email = clientPrincipal.GetEmailFromClaims("github");

        // Assert
        Assert.AreEqual("github-primary@example.com", email);
    }

    [TestMethod]
    public void GetEmailFromClaims_WithDirectEmailClaim_ReturnsEmail()
    {
        // Arrange
        var clientPrincipal = new ClientPrincipal
        {
            IdentityProvider = "github",
            Claims = new[]
            {
                new ClientPrincipalClaim { Type = "email", Value = "direct@example.com" }
            }
        };

        // Act
        var email = clientPrincipal.GetEmailFromClaims("github");

        // Assert
        Assert.AreEqual("direct@example.com", email);
    }

    [TestMethod]
    public void GetEmailFromClaims_WithInvalidEmail_ReturnsNull()
    {
        // Arrange
        var clientPrincipal = new ClientPrincipal
        {
            IdentityProvider = "github",
            Claims = new[]
            {
                new ClientPrincipalClaim { Type = "email", Value = "invalid-email" }
            }
        };

        // Act
        var email = clientPrincipal.GetEmailFromClaims("github");

        // Assert
        Assert.IsNull(email);
    }

    [TestMethod]
    public void GetEmailFromClaims_WithNoEmailClaims_ReturnsNull()
    {
        // Arrange
        var clientPrincipal = new ClientPrincipal
        {
            IdentityProvider = "github",
            Claims = new[]
            {
                new ClientPrincipalClaim { Type = "name", Value = "John Doe" }
            }
        };

        // Act
        var email = clientPrincipal.GetEmailFromClaims("github");

        // Assert
        Assert.IsNull(email);
    }

    [TestMethod]
    public void GetEmailFromClaims_WithNoClaims_ReturnsNull()
    {
        // Arrange
        var clientPrincipal = new ClientPrincipal
        {
            IdentityProvider = "github",
            Claims = null
        };

        // Act
        var email = clientPrincipal.GetEmailFromClaims("github");

        // Assert
        Assert.IsNull(email);
    }

    [TestMethod]
    public void GetEffectiveUserDetails_WithEnhancedEmailParsing_ReturnsGitHubEmail()
    {
        // Arrange
        var clientPrincipal = new ClientPrincipal
        {
            IdentityProvider = "github",
            UserDetails = "", // Empty UserDetails to force claim lookup
            Claims = new[]
            {
                new ClientPrincipalClaim { Type = "urn:github:email", Value = "github@example.com" }
            }
        };

        // Act
        var email = clientPrincipal.GetEffectiveUserDetails();

        // Assert
        Assert.AreEqual("github@example.com", email);
    }

    [TestMethod]
    public void GetEffectiveUserDetails_WithUserDetailsSet_ReturnsUserDetails()
    {
        // Arrange
        var clientPrincipal = new ClientPrincipal
        {
            IdentityProvider = "github",
            UserDetails = "direct@example.com",
            Claims = new[]
            {
                new ClientPrincipalClaim { Type = "urn:github:email", Value = "github@example.com" }
            }
        };

        // Act
        var email = clientPrincipal.GetEffectiveUserDetails();

        // Assert
        Assert.AreEqual("direct@example.com", email);
    }

    [TestMethod]
    public void GetLoginUrl_ForGitHub_IncludesUserEmailScope()
    {
        // Arrange
        var httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        var configuration = Substitute.For<IConfiguration>();
        var httpClientFactory = Substitute.For<IHttpClientFactory>();
        var serviceProvider = Substitute.For<IServiceProvider>();
        var logger = Substitute.For<ILogger<ServerSideAuthService>>();
        var jsRuntime = Substitute.For<IJSRuntime>();

        // Create a real configuration that will work with UserSettingsService
        var configDict = new Dictionary<string, string?>
        {
            { "Authentication:DEBUG", "false" }
        };
        var configBuilder = new ConfigurationBuilder().AddInMemoryCollection(configDict);
        var realConfig = configBuilder.Build();

        var userSettingsService = new UserSettingsService(jsRuntime, realConfig);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Scheme = "https";
        httpContext.Request.Host = new HostString("example.com");
        httpContextAccessor.HttpContext.Returns(httpContext);

        var authService = new ServerSideAuthService(
            httpContextAccessor,
            configuration,
            httpClientFactory,
            serviceProvider,
            logger,
            userSettingsService);

        // Act
        var loginUrl = authService.GetLoginUrl("GitHub", "https://example.com/callback");

        // Assert
        Assert.Contains("scope=user:email", loginUrl, 
            $"Expected GitHub login URL to contain 'scope=user:email', but got: {loginUrl}");
        Assert.Contains("/.auth/login/github", loginUrl, 
            $"Expected GitHub login URL to contain '/.auth/login/github', but got: {loginUrl}");
    }

    [TestMethod]
    public void GetLoginUrl_ForGoogle_DoesNotIncludeGitHubScope()
    {
        // Arrange
        var httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        var configuration = Substitute.For<IConfiguration>();
        var httpClientFactory = Substitute.For<IHttpClientFactory>();
        var serviceProvider = Substitute.For<IServiceProvider>();
        var logger = Substitute.For<ILogger<ServerSideAuthService>>();
        var jsRuntime = Substitute.For<IJSRuntime>();

        // Create a real configuration that will work with UserSettingsService
        var configDict = new Dictionary<string, string?>
        {
            { "Authentication:DEBUG", "false" }
        };
        var configBuilder = new ConfigurationBuilder().AddInMemoryCollection(configDict);
        var realConfig = configBuilder.Build();

        var userSettingsService = new UserSettingsService(jsRuntime, realConfig);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Scheme = "https";
        httpContext.Request.Host = new HostString("example.com");
        httpContextAccessor.HttpContext.Returns(httpContext);

        var authService = new ServerSideAuthService(
            httpContextAccessor,
            configuration,
            httpClientFactory,
            serviceProvider,
            logger,
            userSettingsService);

        // Act
        var loginUrl = authService.GetLoginUrl("Google", "https://example.com/callback");

        // Assert
        Assert.DoesNotContain("scope=user:email", loginUrl, 
            $"Expected Google login URL to not contain GitHub scope, but got: {loginUrl}");
        Assert.Contains("access_type=offline", loginUrl, 
            $"Expected Google login URL to contain 'access_type=offline', but got: {loginUrl}");
    }
}