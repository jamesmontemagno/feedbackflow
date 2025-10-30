# Security Headers Implementation

This document describes the comprehensive security headers implementation for the FeedbackFlow Blazor Server application.

## Overview

The application implements multiple layers of security headers to protect against common web vulnerabilities:

1. **Content Security Policy (CSP)** with nonce support
2. **Subresource Integrity (SRI)** for external CDN resources
3. **HTTP Strict Transport Security (HSTS)**
4. **Permissions Policy** to restrict browser features
5. **Traditional security headers** (X-Frame-Options, X-Content-Type-Options, etc.)

## Implementation Details

### Security Headers Middleware

Located at: `feedbackwebapp/Middleware/SecurityHeadersMiddleware.cs`

The middleware adds the following headers to all HTTP responses:

#### Basic Security Headers

- **X-Content-Type-Options: nosniff**
  - Prevents MIME type sniffing attacks
  - Forces browsers to respect declared content types

- **X-Frame-Options: DENY**
  - Prevents clickjacking attacks
  - Disallows embedding the application in frames/iframes

- **Referrer-Policy: strict-origin-when-cross-origin**
  - Controls referrer information sent with requests
  - Balances privacy with functionality

- **X-XSS-Protection: 1; mode=block**
  - Enables XSS filtering in legacy browsers
  - Blocks page rendering if XSS is detected

#### HTTP Strict Transport Security (HSTS)

```
Strict-Transport-Security: max-age=31536000; includeSubDomains; preload
```

- Only applied in **production** environments over **HTTPS**
- Forces browsers to always use HTTPS for the domain
- Includes all subdomains
- 1-year duration (31536000 seconds)
- Eligible for browser preload lists

**Note**: HSTS is intentionally disabled in development to allow HTTP testing.

#### Permissions Policy

Restricts access to sensitive browser features:

```
Permissions-Policy: geolocation=(), microphone=(), camera=(), payment=(), usb=(), magnetometer=(), gyroscope=(), accelerometer=()
```

This minimizes the attack surface by preventing access to features not needed by the application.

#### Content Security Policy (CSP)

The CSP is tailored for Blazor Server applications with the following directives:

| Directive | Value | Purpose |
|-----------|-------|---------|
| `default-src` | `'self'` | Restrict all resources to same origin by default |
| `script-src` | `'self' 'nonce-XXX' 'unsafe-eval' https://cdn.jsdelivr.net` | Allow scripts from self, with nonce for inline, eval for Blazor SignalR, and CDN |
| `style-src` | `'self' 'unsafe-inline' https://cdn.jsdelivr.net` | Allow styles from self, inline (required by Blazor), and CDN |
| `font-src` | `'self' https://cdn.jsdelivr.net data:` | Allow fonts from self, CDN, and data URIs |
| `img-src` | `'self' data: https:` | Allow images from self, data URIs, and HTTPS sources |
| `connect-src` | `'self' ws: wss:` | Allow connections to self and WebSockets (for SignalR) |
| `frame-ancestors` | `'none'` | Prevent embedding (alternative to X-Frame-Options) |
| `base-uri` | `'self'` | Restrict base tag to prevent injection attacks |
| `form-action` | `'self'` | Restrict form submissions to same origin |
| `object-src` | `'none'` | Block plugins (Flash, Java, etc.) |
| `media-src` | `'self'` | Allow media only from same origin |

**CSP Mode by Environment:**
- **Development**: Report-Only mode (`Content-Security-Policy-Report-Only`)
  - Violations are logged but not blocked
  - Allows safe testing and iteration
- **Production**: Enforcement mode (`Content-Security-Policy`)
  - Violations are blocked
  - Provides active protection

**Nonce Support:**
- A cryptographically secure nonce is generated for each request
- The nonce is available to Blazor components via `ICspNonceService`
- 32-byte random value encoded in Base64

### Subresource Integrity (SRI)

All external CDN resources include integrity checks to prevent tampering:

Located in: `feedbackwebapp/Components/App.razor`

#### Bootstrap CSS 5.3.2
```html
<link href="https://cdn.jsdelivr.net/npm/bootstrap@5.3.2/dist/css/bootstrap.min.css" 
      rel="stylesheet"
      integrity="sha384-T3c6CoIi6uLrA9TneNEoa7RxnatzjcDSCmG1MXxSR1GAsXEV/Dwwykc2MPK8M2HN"
      crossorigin="anonymous">
```

#### Bootstrap Icons 1.11.3
```html
<link href="https://cdn.jsdelivr.net/npm/bootstrap-icons@1.11.3/font/bootstrap-icons.min.css" 
      rel="stylesheet"
      integrity="sha384-tViUnnbYAV00FLIhhi3v/dWt3Jxw4gZQcNoSCxCIFNJVCx7/D55/wXsrNIRANwdD"
      crossorigin="anonymous">
```

#### Bootstrap JS 5.3.2
```html
<script src="https://cdn.jsdelivr.net/npm/bootstrap@5.3.2/dist/js/bootstrap.bundle.min.js"
        integrity="sha384-C6RzsynM9kWDrMNeT87bh95OGNyZPhcTNXj1NW7RuBCsyN/o0jlpcV8Qyq46cDfL"
        crossorigin="anonymous"></script>
```

#### Canvas Confetti 1.6.0
```html
<script src="https://cdn.jsdelivr.net/npm/canvas-confetti@1.6.0/dist/confetti.browser.min.js"
        integrity="sha384-yLVPTWWmjr0TC3wLN3XlAPTJCAGDVT0JV5tGqZVMqHctf7YpvKKLdJZzE5Z4l5G8"
        crossorigin="anonymous"></script>
```

**Note**: Internal static assets (served from `_framework/` and local paths) do NOT use SRI as they are:
1. Served from the same origin (covered by CSP `'self'`)
2. Under our direct control
3. Protected by application integrity checks

## Usage

### Middleware Registration

The middleware is automatically registered in `Program.cs`:

```csharp
app.UseHttpsRedirection();
app.UseSecurityHeaders();  // <-- Security headers middleware
app.UseAntiforgery();
```

**Important**: The middleware must be placed:
- After `UseHttpsRedirection()` (to ensure HTTPS detection works)
- Before `UseAntiforgery()` and routing (to apply headers to all responses)

### Accessing CSP Nonce in Components

If you need to add inline scripts with CSP compliance:

```csharp
@inject ICspNonceService NonceService

<script nonce="@NonceService.GetNonce()">
    // Your inline JavaScript here
</script>
```

## Testing

Comprehensive tests are provided in `feedbackflow.tests/SecurityHeadersMiddlewareTests.cs`:

1. **SecurityHeadersMiddleware_AppliesBasicSecurityHeaders** - Verifies X-Content-Type-Options, X-Frame-Options, Referrer-Policy, X-XSS-Protection
2. **SecurityHeadersMiddleware_AppliesPermissionsPolicy** - Verifies Permissions-Policy restricts features
3. **SecurityHeadersMiddleware_AppliesCSPInDevelopment** - Verifies CSP is in report-only mode in dev
4. **SecurityHeadersMiddleware_AppliesCSPInProduction** - Verifies CSP is enforced in production
5. **SecurityHeadersMiddleware_GeneratesUniqueNoncePerRequest** - Verifies nonce uniqueness
6. **SecurityHeadersMiddleware_DoesNotApplyHSTSInDevelopment** - Verifies HSTS is disabled in dev

Run tests with:
```bash
dotnet test --filter "FullyQualifiedName~SecurityHeadersMiddleware"
```

## Adding New External Resources

When adding new CDN resources, follow these steps:

### 1. Calculate SRI Hash

Use one of these methods:

**Method A: Using OpenSSL (if resource is accessible)**
```bash
curl -s https://cdn.example.com/library.js | openssl dgst -sha384 -binary | openssl base64 -A
```

**Method B: Using online tools**
- https://www.srihash.org/
- Enter the CDN URL to generate the hash

### 2. Add Resource with SRI

```html
<script src="https://cdn.example.com/library.js"
        integrity="sha384-GENERATED_HASH_HERE"
        crossorigin="anonymous"></script>
```

### 3. Update CSP if Needed

If the resource is from a new CDN domain, update the CSP in `SecurityHeadersMiddleware.cs`:

```csharp
// Add the new CDN to script-src or style-src
"script-src 'self' 'nonce-{nonce}' 'unsafe-eval' https://cdn.jsdelivr.net https://cdn.example.com"
```

### 4. Test

- Test in development (CSP report-only mode)
- Check browser console for CSP violations
- Verify resource loads correctly
- Test in production mode (CSP enforcement)

## Monitoring CSP Violations

In development, CSP violations are logged to the browser console:

1. Open browser Developer Tools (F12)
2. Check the Console tab for CSP violation reports
3. Look for messages like: `Refused to load...because it violates the following Content Security Policy directive`

To enable CSP reporting in production, add a `report-uri` or `report-to` directive to the CSP.

## Blazor-Specific Considerations

### Why `'unsafe-eval'` is Required

Blazor SignalR uses dynamic code evaluation for message deserialization. This is a known Blazor requirement:
- https://learn.microsoft.com/aspnet/core/blazor/security/content-security-policy

### Why `'unsafe-inline'` for Styles

Blazor uses inline styles for:
- Scoped CSS isolation
- Dynamic component styling
- CSS variable manipulation

This is acceptable because:
1. Styles have lower security risk than scripts
2. The application controls all component styles
3. External stylesheets still require CSP approval

## References

- [ASP.NET Core CSP Documentation](https://learn.microsoft.com/aspnet/core/security/csp)
- [MDN: Content Security Policy](https://developer.mozilla.org/docs/Web/HTTP/CSP)
- [MDN: Subresource Integrity](https://developer.mozilla.org/docs/Web/Security/Subresource_Integrity)
- [OWASP Secure Headers Project](https://owasp.org/www-project-secure-headers/)
- [MDN: Strict-Transport-Security](https://developer.mozilla.org/docs/Web/HTTP/Headers/Strict-Transport-Security)
- [MDN: Permissions-Policy](https://developer.mozilla.org/docs/Web/HTTP/Headers/Permissions-Policy)

## Troubleshooting

### Resource Not Loading

1. Check browser console for CSP violations
2. Verify SRI hash matches the resource
3. Ensure `crossorigin="anonymous"` is set
4. Check if the CDN domain is allowed in CSP

### HSTS Issues

If you need to test without HTTPS:
1. Use development environment (HSTS is disabled)
2. Clear browser HSTS cache if previously accessed over HTTPS:
   - Chrome: chrome://net-internals/#hsts
   - Firefox: Forget the site and clear cache

### CSP Too Restrictive

If a legitimate resource is blocked:
1. Test in development (report-only mode) first
2. Add the source to the appropriate CSP directive
3. Prefer specific sources over wildcards
4. Consider if the resource is really necessary
