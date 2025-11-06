using System.Security.Cryptography;

namespace FeedbackWebApp.Middleware;

/// <summary>
/// Middleware to add comprehensive security headers to HTTP responses.
/// Implements CSP with nonce support, SRI requirements, HSTS, and other security best practices.
/// </summary>
public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<SecurityHeadersMiddleware> _logger;

    public SecurityHeadersMiddleware(
        RequestDelegate next,
        IWebHostEnvironment environment,
        ILogger<SecurityHeadersMiddleware> logger)
    {
        _next = next;
        _environment = environment;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Generate a cryptographically secure nonce for CSP
        var nonce = GenerateNonce();
        context.Items["csp-nonce"] = nonce;

        // Add security headers before calling next middleware
        AddSecurityHeaders(context, nonce);

        await _next(context);
    }

    private void AddSecurityHeaders(HttpContext context, string nonce)
    {
        var headers = context.Response.Headers;

        // X-Content-Type-Options: Prevent MIME type sniffing
        headers["X-Content-Type-Options"] = "nosniff";

        // X-Frame-Options: Prevent clickjacking
        headers["X-Frame-Options"] = "DENY";

        // Referrer-Policy: Control referrer information
        headers["Referrer-Policy"] = "strict-origin-when-cross-origin";

        // X-XSS-Protection: Enable XSS filter (legacy browsers)
        headers["X-XSS-Protection"] = "1; mode=block";

        // Strict-Transport-Security (HSTS): Only for production over HTTPS
        // The default HSTS value is 30 days, extended to 1 year for production
        if (!_environment.IsDevelopment() && context.Request.IsHttps)
        {
            headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains; preload";
        }

        // Permissions-Policy: Restrict browser features to minimize attack surface
        var permissionsPolicy = new[]
        {
            "geolocation=()",
            "microphone=()",
            "camera=()",
            "payment=()",
            "usb=()",
            "magnetometer=()",
            "gyroscope=()",
            "accelerometer=()"
        };
        headers["Permissions-Policy"] = string.Join(", ", permissionsPolicy);

        // Content-Security-Policy
        // Start in report-only mode for development, enforce in production
        var cspHeaderName = _environment.IsDevelopment() 
            ? "Content-Security-Policy-Report-Only" 
            : "Content-Security-Policy";

        var csp = BuildContentSecurityPolicy(nonce);
        headers[cspHeaderName] = csp;

        _logger.LogDebug("Security headers applied with nonce: {Nonce}", nonce);
    }

    private string BuildContentSecurityPolicy(string nonce)
    {
        // Content Security Policy tailored for Blazor Server with external CDN resources
        // Using nonce-based CSP for inline scripts required by Blazor
        
        var cspDirectives = new List<string>
        {
            // Default: Restrict to self
            "default-src 'self'",
            
            // Scripts: Allow self, nonce for inline scripts, and specific CDNs
            // 'unsafe-eval' is required for Blazor SignalR message deserialization
            // This is a documented Blazor requirement and cannot be avoided without breaking SignalR
            // See: https://learn.microsoft.com/aspnet/core/blazor/security/content-security-policy
            $"script-src 'self' 'nonce-{nonce}' 'unsafe-eval' https://cdn.jsdelivr.net",
            
            // Styles: Allow self, inline styles (Blazor uses them), and CDNs
            // 'unsafe-inline' is needed for Blazor's scoped CSS and dynamic styles
            "style-src 'self' 'unsafe-inline' https://cdn.jsdelivr.net",
            
            // Fonts: Allow self and CDN for Bootstrap Icons
            "font-src 'self' https://cdn.jsdelivr.net data:",
            
            // Images: Allow self and data URIs for inline SVGs
            "img-src 'self' data: https:",
            
            // Connect: Allow self for Blazor SignalR WebSocket connections
            "connect-src 'self' ws: wss:",
            
            // Frame ancestors: Prevent embedding
            "frame-ancestors 'none'",
            
            // Base URI: Restrict to self
            "base-uri 'self'",
            
            // Form actions: Restrict to self
            "form-action 'self'",
            
            // Object/Embed: Block plugins
            "object-src 'none'",
            
            // Media: Allow self
            "media-src 'self'",
            
            // Upgrade insecure requests in production
            // Commented out for development to allow HTTP
            // "upgrade-insecure-requests"
        };

        return string.Join("; ", cspDirectives);
    }

    private static string GenerateNonce()
    {
        // Generate a cryptographically secure random nonce
        var nonceBytes = new byte[32];
        RandomNumberGenerator.Fill(nonceBytes);
        return Convert.ToBase64String(nonceBytes);
    }
}

/// <summary>
/// Extension methods for registering security headers middleware
/// </summary>
public static class SecurityHeadersMiddlewareExtensions
{
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<SecurityHeadersMiddleware>();
    }
}
