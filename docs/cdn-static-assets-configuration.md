# CDN and Static Assets Configuration

This document provides comprehensive guidance for configuring Azure CDN, Azure Front Door, or other CDN providers to optimize static asset delivery for the FeedbackFlow application.

## Overview

FeedbackFlow uses .NET 9's `MapStaticAssets()` feature, which provides:

- **Content-based fingerprinting**: Assets get unique URLs with content hashes (e.g., `css/app.c68w6wcsvq.css`)
- **Build-time compression**: Both Brotli (.br) and Gzip (.gz) variants are pre-generated
- **Automatic cache headers**: Fingerprinted assets get long-lived, immutable cache directives
- **Content-Encoding negotiation**: Appropriate compressed variant served based on Accept-Encoding

## Static Asset Behavior

### Fingerprinted Assets

**URL Pattern**: `{path}/{filename}.{fingerprint}.{extension}`
- Example: `css/app.c68w6wcsvq.css`, `js/app.g7uhp9zbiv.js`

**Cache Headers**:
```
Cache-Control: max-age=31536000, immutable
ETag: "{content-hash}"
Vary: Content-Encoding
```

**Compression**: Both `.br` and `.gz` variants available
- Server selects based on `Accept-Encoding` header
- Brotli preferred when supported (smaller size)

### Non-Fingerprinted Assets

**URL Pattern**: `{path}/{filename}.{extension}`
- Example: `favicon.svg`, `robots.txt`

**Cache Headers**:
```
Cache-Control: no-cache
ETag: "{content-hash}"
```

These assets should be validated on each request but can still benefit from CDN caching with revalidation.

## Azure CDN Configuration

### Option 1: Azure CDN Standard/Premium

#### 1. Create CDN Profile and Endpoint

```bash
# Create resource group (if needed)
az group create --name feedbackflow-cdn-rg --location eastus

# Create CDN profile
az cdn profile create \
  --resource-group feedbackflow-cdn-rg \
  --name feedbackflow-cdn \
  --sku Standard_Microsoft

# Create CDN endpoint
az cdn endpoint create \
  --resource-group feedbackflow-cdn-rg \
  --profile-name feedbackflow-cdn \
  --name feedbackflow \
  --origin feedbackwebapp20250414225345.azurewebsites.net \
  --origin-host-header feedbackwebapp20250414225345.azurewebsites.net \
  --enable-compression true
```

#### 2. Configure Compression

Enable compression for common file types:
```bash
az cdn endpoint update \
  --resource-group feedbackflow-cdn-rg \
  --profile-name feedbackflow-cdn \
  --name feedbackflow \
  --content-types-to-compress \
    "text/html" \
    "text/css" \
    "application/javascript" \
    "text/javascript" \
    "application/json" \
    "image/svg+xml"
```

> **Note**: Since our app pre-generates compressed files, the CDN compression setting is a fallback. The app will serve `.br` or `.gz` files directly based on Accept-Encoding.

#### 3. Configure Caching Rules

Create caching rules to respect origin cache headers:

**Rule 1: Fingerprinted Assets (Long Cache)**
```bash
az cdn endpoint rule add \
  --resource-group feedbackflow-cdn-rg \
  --profile-name feedbackflow-cdn \
  --endpoint-name feedbackflow \
  --order 1 \
  --rule-name "FingerprintedAssets" \
  --match-variable UrlPath \
  --operator Contains \
  --match-values "." \
  --action-name CacheExpiration \
  --cache-behavior Override \
  --cache-duration "365.00:00:00"
```

**Rule 2: HTML and Dynamic Content (Short Cache)**
```bash
az cdn endpoint rule add \
  --resource-group feedbackflow-cdn-rg \
  --profile-name feedbackflow-cdn \
  --endpoint-name feedbackflow \
  --order 2 \
  --rule-name "HtmlContent" \
  --match-variable UrlFileExtension \
  --operator Equal \
  --match-values "html" "" \
  --action-name CacheExpiration \
  --cache-behavior Override \
  --cache-duration "00:05:00"
```

#### 4. Query String Caching

Configure query string behavior:
```bash
az cdn endpoint update \
  --resource-group feedbackflow-cdn-rg \
  --profile-name feedbackflow-cdn \
  --name feedbackflow \
  --query-string-caching-behavior IgnoreQueryString
```

For fingerprinted assets, query strings are not used, so this can be ignored.

### Option 2: Azure Front Door

Azure Front Door provides enhanced features including:
- Advanced routing and load balancing
- Web Application Firewall (WAF)
- Better global distribution
- More granular caching rules

#### 1. Create Front Door Profile

```bash
az afd profile create \
  --resource-group feedbackflow-cdn-rg \
  --profile-name feedbackflow-frontdoor \
  --sku Premium_AzureFrontDoor
```

#### 2. Create Origin Group and Origin

```bash
# Create origin group
az afd origin-group create \
  --resource-group feedbackflow-cdn-rg \
  --profile-name feedbackflow-frontdoor \
  --origin-group-name webapp-origin-group \
  --probe-request-type GET \
  --probe-protocol Https \
  --probe-interval-in-seconds 30 \
  --probe-path "/" \
  --sample-size 4 \
  --successful-samples-required 3 \
  --additional-latency-in-milliseconds 50

# Add origin
az afd origin create \
  --resource-group feedbackflow-cdn-rg \
  --profile-name feedbackflow-frontdoor \
  --origin-group-name webapp-origin-group \
  --origin-name webapp-origin \
  --host-name feedbackwebapp20250414225345.azurewebsites.net \
  --origin-host-header feedbackwebapp20250414225345.azurewebsites.net \
  --priority 1 \
  --weight 1000 \
  --enabled-state Enabled \
  --http-port 80 \
  --https-port 443
```

#### 3. Create Endpoint and Route

```bash
# Create endpoint
az afd endpoint create \
  --resource-group feedbackflow-cdn-rg \
  --profile-name feedbackflow-frontdoor \
  --endpoint-name feedbackflow-endpoint \
  --enabled-state Enabled

# Create route
az afd route create \
  --resource-group feedbackflow-cdn-rg \
  --profile-name feedbackflow-frontdoor \
  --endpoint-name feedbackflow-endpoint \
  --route-name default-route \
  --origin-group webapp-origin-group \
  --supported-protocols Http Https \
  --patterns-to-match "/*" \
  --forwarding-protocol HttpsOnly \
  --https-redirect Enabled \
  --enable-compression true
```

#### 4. Configure Rule Set for Cache Behavior

Create a rule set for advanced caching:

```bash
# Create rule set
az afd rule-set create \
  --resource-group feedbackflow-cdn-rg \
  --profile-name feedbackflow-frontdoor \
  --rule-set-name CacheRules

# Rule 1: Fingerprinted static assets - long cache
az afd rule create \
  --resource-group feedbackflow-cdn-rg \
  --profile-name feedbackflow-frontdoor \
  --rule-set-name CacheRules \
  --rule-name FingerprintedAssets \
  --order 1 \
  --match-variable UrlPath \
  --operator Contains \
  --match-values "." \
  --action-name CacheOverride \
  --cache-behavior OverrideIfOriginMissing \
  --cache-duration 365.00:00:00

# Rule 2: Non-fingerprinted assets - respect origin
az afd rule create \
  --resource-group feedbackflow-cdn-rg \
  --profile-name feedbackflow-frontdoor \
  --rule-set-name CacheRules \
  --rule-name NonFingerprintedAssets \
  --order 2 \
  --match-variable UrlFileExtension \
  --operator Equal \
  --match-values "svg" "txt" \
  --action-name CacheOverride \
  --cache-behavior HonorOrigin
```

## Cache Purging Strategy

### When to Purge

Cache purging is typically needed when:
1. Deploying a new version of the application
2. Making emergency hotfixes
3. Updating non-fingerprinted assets

### Automatic Purge on Deployment

Add a purge step to your deployment workflow:

#### Azure CDN Purge

Add to `.github/workflows/main_feedbackwebapp20250414225345.yml`:

```yaml
- name: Purge CDN cache
  if: success()
  run: |
    az cdn endpoint purge \
      --resource-group feedbackflow-cdn-rg \
      --profile-name feedbackflow-cdn \
      --name feedbackflow \
      --content-paths "/*" "/*.html" "/favicon.svg"
  env:
    AZURE_CLIENT_ID: ${{ secrets.AZUREAPPSERVICE_CLIENTID_76B1871B1DD14EA2BEF81BD46B0B7298 }}
    AZURE_TENANT_ID: ${{ secrets.AZUREAPPSERVICE_TENANTID_7994343D83D544D7B91D9E9E656BDBBE }}
    AZURE_SUBSCRIPTION_ID: ${{ secrets.AZUREAPPSERVICE_SUBSCRIPTIONID_A1D5E2DC840E4D0AA80422C02B7D3443 }}
```

#### Azure Front Door Purge

```yaml
- name: Purge Front Door cache
  if: success()
  run: |
    az afd endpoint purge \
      --resource-group feedbackflow-cdn-rg \
      --profile-name feedbackflow-frontdoor \
      --endpoint-name feedbackflow-endpoint \
      --content-paths "/*" "/*.html"
  env:
    AZURE_CLIENT_ID: ${{ secrets.AZUREAPPSERVICE_CLIENTID_76B1871B1DD14EA2BEF81BD46B0B7298 }}
    AZURE_TENANT_ID: ${{ secrets.AZUREAPPSERVICE_TENANTID_7994343D83D544D7B91D9E9E656BDBBE }}
    AZURE_SUBSCRIPTION_ID: ${{ secrets.AZUREAPPSERVICE_SUBSCRIPTIONID_A1D5E2DC840E4D0AA80422C02B7D3443 }}
```

### Selective Purging

**Fingerprinted assets**: Generally don't need purging since the URL changes with content.

**Non-fingerprinted assets**: Purge specific paths:
```bash
# Purge specific files
az cdn endpoint purge \
  --resource-group feedbackflow-cdn-rg \
  --profile-name feedbackflow-cdn \
  --name feedbackflow \
  --content-paths "/favicon.svg" "/robots.txt" "/*.html"
```

## Content-Encoding Negotiation

The application automatically handles Content-Encoding negotiation through the static assets middleware.

### How It Works

1. **Client sends request** with `Accept-Encoding: br, gzip, deflate`
2. **Server checks manifest** (`WebApp.staticwebassets.endpoints.json`)
3. **Server selects best match**:
   - Prefers Brotli (`.br`) if client supports it
   - Falls back to Gzip (`.gz`) if client only supports gzip
   - Serves uncompressed if no compression supported
4. **Server responds** with appropriate `Content-Encoding` header

### CDN Considerations

- **Respect Vary Header**: CDN must cache different versions based on `Vary: Content-Encoding`
- **Pass-through Accept-Encoding**: CDN should forward the `Accept-Encoding` header to origin
- **Cache by Encoding**: CDN should maintain separate cache entries for br/gzip/uncompressed

### Azure CDN/Front Door Default Behavior

Both Azure CDN and Front Door automatically:
- Respect `Vary` headers
- Forward compression-related headers
- Cache different encoding variants separately

No additional configuration needed!

## Monitoring and Verification

### Verify Compression

Test compression with curl:

```bash
# Test Brotli
curl -H "Accept-Encoding: br" \
  -I https://your-cdn-endpoint.azurefd.net/css/app.css

# Expected headers:
# Content-Encoding: br
# Cache-Control: no-cache (or max-age=31536000, immutable for fingerprinted)
# Vary: Content-Encoding

# Test Gzip
curl -H "Accept-Encoding: gzip" \
  -I https://your-cdn-endpoint.azurefd.net/css/app.css
```

### Verify Fingerprinted Assets

```bash
# Check fingerprinted URL
curl -I https://your-cdn-endpoint.azurefd.net/css/app.c68w6wcsvq.css

# Expected headers:
# Cache-Control: max-age=31536000, immutable
# ETag: "..."
```

### Monitor Cache Hit Ratio

Use Azure Monitor to track:
- **Cache Hit Ratio**: Should be >90% for static assets
- **Origin Request Rate**: Should be low for fingerprinted assets
- **Data Transfer**: Verify compressed transfer savings

## Security Considerations

### HTTPS Only

Ensure all CDN/Front Door routes enforce HTTPS:
- Set `--https-redirect Enabled` on routes
- Configure custom domains with valid TLS certificates

### Content Security Policy

Add CSP headers for static assets:

```csharp
// In Program.cs, after app.UseHttpsRedirection();
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/css") ||
        context.Request.Path.StartsWithSegments("/js") ||
        context.Request.Path.StartsWithSegments("/_content"))
    {
        context.Response.Headers.Add("X-Content-Type-Options", "nosniff");
        context.Response.Headers.Add("X-Frame-Options", "DENY");
    }
    await next();
});
```

### SRI (Subresource Integrity)

The static assets manifest includes integrity hashes that can be used for SRI:

```json
{
  "EndpointProperties": [
    {
      "Name": "integrity",
      "Value": "sha256-nAgwVsWbMOWaYQ5Lg2k3rPOEoaFnk8Qi7iFKMfSuYSs="
    }
  ]
}
```

Consider implementing SRI in Blazor components for external libraries.

## Troubleshooting

### Assets Not Compressed

**Problem**: Assets served uncompressed even with proper Accept-Encoding.

**Solution**:
1. Verify publish output includes `.br` and `.gz` files
2. Check `WebApp.staticwebassets.endpoints.json` exists
3. Ensure CDN forwards `Accept-Encoding` header to origin
4. Verify `MapStaticAssets()` is called in Program.cs

### Cache Not Working

**Problem**: Assets not being cached or always requesting origin.

**Solution**:
1. Verify `Cache-Control` headers in response
2. Check CDN caching rules are configured
3. Ensure query string caching is set appropriately
4. Verify `Vary: Content-Encoding` is respected by CDN

### Wrong Compression Format

**Problem**: Gzip served instead of Brotli or vice versa.

**Solution**:
1. Check client `Accept-Encoding` header
2. Verify CDN is passing through the header
3. Check static assets manifest has both variants
4. Test direct origin without CDN to isolate issue

### High Origin Requests

**Problem**: Too many requests hitting origin instead of cache.

**Solution**:
1. Verify fingerprinted URLs are being used in HTML
2. Check cache TTL settings
3. Monitor cache purge frequency (avoid over-purging)
4. Verify CDN rules don't bypass cache for static assets

## Performance Benchmarks

Expected improvements with proper CDN configuration:

- **File Size Reduction**: 70-80% with Brotli compression
- **Cache Hit Ratio**: 95%+ for static assets
- **Time to First Byte (TTFB)**: <50ms from CDN edge
- **Page Load Time**: 30-50% improvement vs uncached origin

## Best Practices Summary

1. ✅ **Use fingerprinted URLs** for all static assets
2. ✅ **Enable both Brotli and Gzip** compression at build time
3. ✅ **Configure long cache TTLs** (1 year) for fingerprinted assets
4. ✅ **Respect origin cache headers** for non-fingerprinted assets
5. ✅ **Implement selective cache purging** on deployment
6. ✅ **Monitor cache hit ratios** and origin request rates
7. ✅ **Use HTTPS only** for all CDN/Front Door endpoints
8. ✅ **Forward compression headers** from CDN to origin
9. ✅ **Test compression** in multiple browsers and clients
10. ✅ **Automate cache purging** in CI/CD pipeline

## Additional Resources

- [.NET 9 MapStaticAssets Documentation](https://learn.microsoft.com/en-us/aspnet/core/blazor/fundamentals/static-files)
- [Azure CDN Documentation](https://learn.microsoft.com/en-us/azure/cdn/)
- [Azure Front Door Documentation](https://learn.microsoft.com/en-us/azure/frontdoor/)
- [Brotli Compression](https://github.com/google/brotli)
- [HTTP Caching Best Practices](https://developer.mozilla.org/en-US/docs/Web/HTTP/Caching)

---

**Last Updated**: 2025-10-30  
**Related Documents**: 
- [Deployment Playbook](./deployment-playbook.md)
- [Static Assets Build Pipeline](./static-assets-build-pipeline.md)
