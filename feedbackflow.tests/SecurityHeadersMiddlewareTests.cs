using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using FeedbackWebApp.Middleware;

namespace FeedbackFlow.Tests;

/// <summary>
/// Tests for security headers middleware to ensure proper CSP, HSTS, and other security headers are applied.
/// </summary>
[TestClass]
public class SecurityHeadersMiddlewareTests
{
    private TestServer CreateTestServer(bool isDevelopment = true)
    {
        var hostBuilder = new HostBuilder()
            .ConfigureWebHost(webHost =>
            {
                webHost.UseTestServer();
                webHost.UseEnvironment(isDevelopment ? "Development" : "Production");
                webHost.ConfigureServices(services =>
                {
                    services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
                    services.AddLogging();
                });
                webHost.Configure(app =>
                {
                    app.UseMiddleware<SecurityHeadersMiddleware>();
                    app.Run(async context =>
                    {
                        await context.Response.WriteAsync("OK");
                    });
                });
            })
            .Build();

        hostBuilder.Start();
        return hostBuilder.GetTestServer();
    }

    [TestMethod]
    public async Task SecurityHeadersMiddleware_AppliesBasicSecurityHeaders()
    {
        // Arrange
        using var server = CreateTestServer(isDevelopment: true);
        var client = server.CreateClient();

        // Act
        var response = await client.GetAsync("/");

        // Assert
        Assert.IsTrue(response.Headers.Contains("X-Content-Type-Options"));
        Assert.AreEqual("nosniff", response.Headers.GetValues("X-Content-Type-Options").First());

        Assert.IsTrue(response.Headers.Contains("X-Frame-Options"));
        Assert.AreEqual("DENY", response.Headers.GetValues("X-Frame-Options").First());

        Assert.IsTrue(response.Headers.Contains("Referrer-Policy"));
        Assert.AreEqual("strict-origin-when-cross-origin", response.Headers.GetValues("Referrer-Policy").First());

        Assert.IsTrue(response.Headers.Contains("X-XSS-Protection"));
        Assert.AreEqual("1; mode=block", response.Headers.GetValues("X-XSS-Protection").First());
    }

    [TestMethod]
    public async Task SecurityHeadersMiddleware_AppliesPermissionsPolicy()
    {
        // Arrange
        using var server = CreateTestServer(isDevelopment: true);
        var client = server.CreateClient();

        // Act
        var response = await client.GetAsync("/");

        // Assert
        Assert.IsTrue(response.Headers.Contains("Permissions-Policy"));
        var permissionsPolicy = response.Headers.GetValues("Permissions-Policy").First();
        
        // Verify that critical features are restricted
        Assert.IsTrue(permissionsPolicy.Contains("geolocation=()"));
        Assert.IsTrue(permissionsPolicy.Contains("microphone=()"));
        Assert.IsTrue(permissionsPolicy.Contains("camera=()"));
    }

    [TestMethod]
    public async Task SecurityHeadersMiddleware_AppliesCSPInDevelopment()
    {
        // Arrange
        using var server = CreateTestServer(isDevelopment: true);
        var client = server.CreateClient();

        // Act
        var response = await client.GetAsync("/");

        // Assert
        // In development, CSP should be in report-only mode
        Assert.IsTrue(response.Headers.Contains("Content-Security-Policy-Report-Only"));
        var csp = response.Headers.GetValues("Content-Security-Policy-Report-Only").First();
        
        // Verify key CSP directives
        Assert.IsTrue(csp.Contains("default-src 'self'"));
        Assert.IsTrue(csp.Contains("script-src"));
        Assert.IsTrue(csp.Contains("'unsafe-eval'")); // Required for Blazor
        Assert.IsTrue(csp.Contains("https://cdn.jsdelivr.net"));
        Assert.IsTrue(csp.Contains("frame-ancestors 'none'"));
        Assert.IsTrue(csp.Contains("object-src 'none'"));
    }

    [TestMethod]
    public async Task SecurityHeadersMiddleware_AppliesCSPInProduction()
    {
        // Arrange
        using var server = CreateTestServer(isDevelopment: false);
        var client = server.CreateClient();

        // Act
        var response = await client.GetAsync("/");

        // Assert
        // In production, CSP should be enforced (not report-only)
        Assert.IsTrue(response.Headers.Contains("Content-Security-Policy"));
        Assert.IsFalse(response.Headers.Contains("Content-Security-Policy-Report-Only"));
        
        var csp = response.Headers.GetValues("Content-Security-Policy").First();
        Assert.IsTrue(csp.Contains("default-src 'self'"));
    }

    [TestMethod]
    public async Task SecurityHeadersMiddleware_GeneratesUniqueNoncePerRequest()
    {
        // Arrange
        using var server = CreateTestServer(isDevelopment: true);
        var client = server.CreateClient();

        // Act
        var response1 = await client.GetAsync("/");
        var response2 = await client.GetAsync("/");

        // Assert
        var csp1 = response1.Headers.GetValues("Content-Security-Policy-Report-Only").First();
        var csp2 = response2.Headers.GetValues("Content-Security-Policy-Report-Only").First();
        
        // Extract nonces from CSP headers
        var nonce1 = ExtractNonce(csp1);
        var nonce2 = ExtractNonce(csp2);
        
        Assert.IsNotNull(nonce1);
        Assert.IsNotNull(nonce2);
        Assert.AreNotEqual(nonce1, nonce2, "Nonces should be unique per request");
    }

    [TestMethod]
    public async Task SecurityHeadersMiddleware_DoesNotApplyHSTSInDevelopment()
    {
        // Arrange
        using var server = CreateTestServer(isDevelopment: true);
        var client = server.CreateClient();

        // Act
        var response = await client.GetAsync("/");

        // Assert
        // HSTS should not be applied in development
        Assert.IsFalse(response.Headers.Contains("Strict-Transport-Security"));
    }

    private string? ExtractNonce(string csp)
    {
        // Extract nonce value from CSP header
        // Expected format: "script-src 'self' 'nonce-ABC123' ..."
        var match = System.Text.RegularExpressions.Regex.Match(csp, @"'nonce-([^']+)'");
        return match.Success ? match.Groups[1].Value : null;
    }
}
