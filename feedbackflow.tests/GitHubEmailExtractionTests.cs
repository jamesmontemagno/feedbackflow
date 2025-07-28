using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharedDump.Models.Authentication;

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
}