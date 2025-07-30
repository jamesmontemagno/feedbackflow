using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharedDump.Models.Authentication;

namespace FeedbackFlow.Tests;

[TestClass]
public class AuthenticationEmailIntegrationTests
{
    [TestMethod]
    public void GitHubAuthFlow_WithEmailScope_RetrievesEmailCorrectly()
    {
        // This test simulates what would happen when Azure Easy Auth 
        // is configured with the "user:email" scope for GitHub
        
        // Arrange: Create a realistic GitHub authentication response
        var clientPrincipal = new ClientPrincipal
        {
            IdentityProvider = "github",
            AuthType = "github", // Alternative property used by Azure Easy Auth
            UserId = "123456",
            UserDetails = "", // Often empty, forcing fallback to claims
            Claims = new[]
            {
                new ClientPrincipalClaim { Type = "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier", Value = "123456" },
                new ClientPrincipalClaim { Type = "urn:github:login", Value = "testuser" },
                new ClientPrincipalClaim { Type = "urn:github:name", Value = "Test User" },
                new ClientPrincipalClaim { Type = "urn:github:email", Value = "testuser@example.com" }, // This is what we want
                new ClientPrincipalClaim { Type = "urn:github:avatar_url", Value = "https://github.com/avatar.png" }
            }
        };

        // Act: Extract email using the enhanced logic
        var email = clientPrincipal.GetEffectiveUserDetails();

        // Assert: Should retrieve the GitHub email
        Assert.AreEqual("testuser@example.com", email);
    }

    [TestMethod]
    public void GitHubAuthFlow_WithoutEmailScope_FallsBackGracefully()
    {
        // This test simulates what happens when "user:email" scope is NOT configured
        
        // Arrange: GitHub auth without email claims
        var clientPrincipal = new ClientPrincipal
        {
            IdentityProvider = "github",
            UserId = "123456",
            UserDetails = "",
            Claims = new[]
            {
                new ClientPrincipalClaim { Type = "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier", Value = "123456" },
                new ClientPrincipalClaim { Type = "urn:github:login", Value = "testuser" },
                new ClientPrincipalClaim { Type = "urn:github:name", Value = "Test User" },
                // No email claims - simulates user with private email or missing scope
            }
        };

        // Act: Attempt to extract email
        var email = clientPrincipal.GetEffectiveUserDetails();

        // Assert: Should return empty string (not null) when no email is found
        Assert.AreEqual("", email);
    }

    [TestMethod]
    public void GitHubAuthFlow_WithPrivateEmail_HandlesGracefully()
    {
        // GitHub users can set their email to private, which may result in different claim patterns
        
        // Arrange: Simulate private email scenario
        var clientPrincipal = new ClientPrincipal
        {
            IdentityProvider = "github",
            UserId = "123456",
            UserDetails = "",
            Claims = new[]
            {
                new ClientPrincipalClaim { Type = "urn:github:login", Value = "testuser" },
                new ClientPrincipalClaim { Type = "email", Value = "123456+testuser@users.noreply.github.com" }, // GitHub's noreply email
            }
        };

        // Act: Extract email
        var email = clientPrincipal.GetEffectiveUserDetails();

        // Assert: Should still extract the noreply email (it's technically valid)
        Assert.AreEqual("123456+testuser@users.noreply.github.com", email);
    }

    [TestMethod]
    public void GitHubAuthFlow_WithMultipleEmailClaims_SelectsPrimary()
    {
        // Some users might have multiple email claims; we should prioritize correctly
        
        // Arrange: Multiple email claims with GitHub-specific taking precedence
        var clientPrincipal = new ClientPrincipal
        {
            IdentityProvider = "github",
            UserId = "123456",
            UserDetails = "",
            Claims = new[]
            {
                new ClientPrincipalClaim { Type = "email", Value = "secondary@example.com" },
                new ClientPrincipalClaim { Type = "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress", Value = "standard@example.com" },
                new ClientPrincipalClaim { Type = "urn:github:primary_email", Value = "primary@example.com" }, // This should win
            }
        };

        // Act: Extract email
        var email = clientPrincipal.GetEffectiveUserDetails();

        // Assert: Should prioritize GitHub-specific primary email claim
        Assert.AreEqual("primary@example.com", email);
    }

    [TestMethod]
    public void AuthenticationMiddleware_Pattern_WorksWithEnhancedParsing()
    {
        // Test the pattern used in AuthenticationMiddleware.cs
        
        // Arrange: Similar to what middleware would receive
        var clientPrincipal = new ClientPrincipal
        {
            IdentityProvider = "github",
            UserId = "github-user-123",
            UserDetails = "", // Empty, forcing claim lookup
            Claims = new[]
            {
                new ClientPrincipalClaim { Type = "urn:github:login", Value = "github-user" },
                new ClientPrincipalClaim { Type = "urn:github:email", Value = "github-user@example.com" },
            }
        };

        // Act: Use the same pattern as AuthenticationMiddleware
        var effectiveProvider = clientPrincipal.GetEffectiveIdentityProvider();
        var effectiveUserId = clientPrincipal.GetEffectiveUserId();
        var email = clientPrincipal.GetEffectiveUserDetails();

        // Assert: All values should be extracted correctly
        Assert.AreEqual("github", effectiveProvider);
        Assert.AreEqual("github-user-123", effectiveUserId);
        Assert.AreEqual("github-user@example.com", email);
    }
}