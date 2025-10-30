# Static Assets Build Pipeline

This document explains how static assets are optimized during the build and publish process using .NET 9's `MapStaticAssets()` feature.

## Overview

FeedbackFlow leverages ASP.NET Core 9.0's static assets optimization, which provides:

1. **Content-based fingerprinting**: Unique URLs based on file content
2. **Build-time compression**: Brotli and Gzip variants pre-generated
3. **Automatic cache headers**: Immutable caching for fingerprinted assets
4. **Content negotiation**: Serves optimal compression based on Accept-Encoding

## Build Pipeline Architecture

```
Source Files                  Build Process                   Publish Output
─────────────                ──────────────                  ───────────────

wwwroot/
  css/
    app.css          ──→    [Analyze Content]     ──→      wwwroot/
    buttons.css      ──→    [Calculate Hash]      ──→        css/
  js/                ──→    [Generate Routes]     ──→          app.css
    app.js           ──→    [Compress Brotli]     ──→          app.c68w6wcsvq.css
    toastService.js  ──→    [Compress Gzip]       ──→          app.c68w6wcsvq.css.br
                                                               app.c68w6wcsvq.css.gz
                                                               app.css
                                                               app.css.br
                                                               app.css.gz
                                                             js/
                                                               app.js
                                                               app.g7uhp9zbiv.js
                                                               app.g7uhp9zbiv.js.br
                                                               app.g7uhp9zbiv.js.gz
                                                         WebApp.staticwebassets.endpoints.json
```

## Configuration

### Program.cs Setup

The static assets middleware is configured in `feedbackwebapp/Program.cs`:

```csharp
var app = builder.Build();

// ... other middleware ...

app.UseHttpsRedirection();
app.UseAntiforgery();

// Enable static assets with fingerprinting and compression
app.MapStaticAssets();

// ... other routes ...

app.Run();
```

### Project Configuration

The feature is automatically enabled for .NET 9 web projects. No additional configuration is needed in `WebApp.csproj`.

## Build Process Details

### Phase 1: Content Analysis

During the build, ASP.NET Core:

1. **Scans wwwroot directory** for static files
2. **Computes SHA-256 hash** of each file's content
3. **Generates base64 fingerprint** (10 characters)
4. **Creates routing table** mapping original paths to fingerprinted URLs

Example:
```
Original:      css/app.css
Content Hash:  9C0830....  (SHA-256)
Fingerprint:   c68w6wcsvq   (base64, 10 chars)
Final URL:     css/app.c68w6wcsvq.css
```

### Phase 2: Compression

For each file, the build process:

1. **Generates Brotli variant** (`.br` extension)
   - Compression level: 4-6 (balanced)
   - Average reduction: 70-80%
   - Preferred for modern browsers

2. **Generates Gzip variant** (`.gz` extension)
   - Compression level: 6 (default)
   - Average reduction: 60-70%
   - Fallback for older browsers

3. **Keeps original** uncompressed file
   - Used when client doesn't support compression
   - Also serves as source for integrity checking

### Phase 3: Manifest Generation

Creates `WebApp.staticwebassets.endpoints.json`:

```json
{
  "Version": 1,
  "ManifestType": "Publish",
  "Endpoints": [
    {
      "Route": "css/app.c68w6wcsvq.css",
      "AssetFile": "css/app.css.br",
      "Selectors": [
        {
          "Name": "Content-Encoding",
          "Value": "br",
          "Quality": 0.000115393492
        }
      ],
      "ResponseHeaders": [
        {
          "Name": "Cache-Control",
          "Value": "max-age=31536000, immutable"
        },
        {
          "Name": "Content-Encoding",
          "Value": "br"
        },
        {
          "Name": "Vary",
          "Value": "Content-Encoding"
        }
      ],
      "EndpointProperties": [
        {
          "Name": "fingerprint",
          "Value": "c68w6wcsvq"
        },
        {
          "Name": "integrity",
          "Value": "sha256-nAgwVsWbMOWaYQ5Lg2k3rPOEoaFnk8Qi7iFKMfSuYSs="
        }
      ]
    }
    // ... more endpoints ...
  ]
}
```

## Runtime Behavior

### Request Processing

When a request comes in for a static asset:

1. **Route Matching**
   - Middleware checks if URL matches a registered endpoint
   - Both fingerprinted and non-fingerprinted URLs supported

2. **Content Negotiation**
   - Reads `Accept-Encoding` header from request
   - Checks available variants: `br`, `gzip`, uncompressed
   - Selects best match based on quality values

3. **Response Generation**
   - Serves appropriate file variant
   - Adds headers from manifest (Cache-Control, ETag, etc.)
   - Sets Content-Encoding header
   - Adds Vary header for proper caching

### Cache Strategy

#### Fingerprinted Assets (Immutable)

**URL Pattern**: `css/app.c68w6wcsvq.css`

**Headers**:
```http
Cache-Control: max-age=31536000, immutable
ETag: "nAgwVsWbMOWaYQ5Lg2k3rPOEoaFnk8Qi7iFKMfSuYSs="
Content-Encoding: br
Vary: Content-Encoding
```

**Behavior**:
- Cached for 1 year (31,536,000 seconds)
- `immutable` directive tells browser to never revalidate
- Perfect for long-term caching
- URL changes when content changes

#### Non-Fingerprinted Assets (Revalidate)

**URL Pattern**: `css/app.css`

**Headers**:
```http
Cache-Control: no-cache
ETag: "nAgwVsWbMOWaYQ5Lg2k3rPOEoaFnk8Qi7iFKMfSuYSs="
Content-Encoding: br
Vary: Content-Encoding
```

**Behavior**:
- Must revalidate with server on each request
- Uses ETag for efficient revalidation (304 Not Modified)
- Good for files that might change but don't use fingerprinting
- Examples: favicon.svg, robots.txt

## CI/CD Integration

### Build Workflow Validation

The build workflow (`.github/workflows/build.yaml`) includes validation:

```yaml
- name: Verify static assets optimization
  shell: bash
  run: |
    echo "🔍 Verifying static assets optimization..."
    
    # Check for fingerprinted assets
    FINGERPRINTED_CSS=$(find ./publish-validation/wwwroot -name "*.*.css" ! -name "*.br" ! -name "*.gz" | wc -l)
    FINGERPRINTED_JS=$(find ./publish-validation/wwwroot -name "*.*.js" ! -name "*.br" ! -name "*.gz" | wc -l)
    
    if [ "$FINGERPRINTED_CSS" -eq 0 ] || [ "$FINGERPRINTED_JS" -eq 0 ]; then
      echo "❌ Error: No fingerprinted assets found!"
      exit 1
    fi
    
    # Check for Brotli and Gzip files
    BR_FILES=$(find ./publish-validation/wwwroot -name "*.br" | wc -l)
    GZ_FILES=$(find ./publish-validation/wwwroot -name "*.gz" | wc -l)
    
    if [ "$BR_FILES" -eq 0 ] || [ "$GZ_FILES" -eq 0 ]; then
      echo "❌ Error: Missing compressed files!"
      exit 1
    fi
    
    # Verify manifest exists
    if [ ! -f "./publish-validation/WebApp.staticwebassets.endpoints.json" ]; then
      echo "❌ Error: Static assets manifest not found!"
      exit 1
    fi
    
    echo "✅ Static assets optimization verified!"
```

### Deployment Workflow Verification

Deployment workflows verify assets before uploading:

```yaml
- name: Verify static assets in publish output
  shell: bash
  run: |
    echo "🔍 Verifying static assets in publish output..."
    BR_COUNT=$(find ./publish/wwwroot -name "*.br" 2>/dev/null | wc -l)
    GZ_COUNT=$(find ./publish/wwwroot -name "*.gz" 2>/dev/null | wc -l)
    echo "Found $BR_COUNT Brotli files and $GZ_COUNT Gzip files"
    
    if [ "$BR_COUNT" -eq 0 ] || [ "$GZ_COUNT" -eq 0 ]; then
      echo "⚠️  Warning: Precompressed assets may be missing"
    else
      echo "✅ Precompressed assets verified"
    fi
```

## Performance Characteristics

### File Size Comparison

Example from actual build output:

| File | Original | Gzip | Brotli | Savings (Brotli) |
|------|----------|------|--------|------------------|
| app.css | 45,566 bytes | 10,218 bytes | 8,665 bytes | 81% |
| app.js | 7,534 bytes | 2,215 bytes | 1,835 bytes | 76% |
| buttons.css | 3,080 bytes | 791 bytes | 625 bytes | 80% |
| WebApp.styles.css | 299,887 bytes | 40,827 bytes | 32,396 bytes | 89% |

**Average Compression Ratio**:
- Gzip: 60-70% reduction
- Brotli: 70-80% reduction

### Caching Performance

**First Visit** (Cache Miss):
```
Request: GET /css/app.c68w6wcsvq.css
         Accept-Encoding: br, gzip

Response: 200 OK
          Content-Length: 8665 (Brotli)
          Cache-Control: max-age=31536000, immutable
          Content-Encoding: br
```

**Subsequent Visits** (Cache Hit):
```
Request: GET /css/app.c68w6wcsvq.css
         If-None-Match: "8tWwldJbp97n9xr0y89s/..."

Response: 200 OK (from cache - no network request)
```

**Benefits**:
- Reduced network latency: 0ms (served from cache)
- Reduced bandwidth: 0 bytes transferred
- Faster page loads: Instant resource availability

## Troubleshooting

### Issue: Fingerprints Not Generated

**Check 1: Verify MapStaticAssets() called**
```csharp
// In Program.cs
app.MapStaticAssets(); // Must be called before MapRazorComponents
```

**Check 2: Build configuration**
```bash
# Must use Release configuration
dotnet publish -c Release
```

**Check 3: Project SDK**
```xml
<!-- WebApp.csproj - must be Web SDK -->
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
  </PropertyGroup>
</Project>
```

### Issue: Compression Not Working

**Check 1: Verify compressed files exist**
```bash
ls -la ./publish/wwwroot/css/
# Should see: app.css, app.css.br, app.css.gz
```

**Check 2: Check Accept-Encoding header**
```bash
curl -H "Accept-Encoding: br, gzip" -I https://yourapp.com/css/app.css
```

**Check 3: Verify manifest**
```bash
cat ./publish/WebApp.staticwebassets.endpoints.json | grep -A 5 "Content-Encoding"
```

### Issue: Wrong Cache Headers

**Check manifest content**:
```bash
# Should show max-age=31536000 for fingerprinted assets
cat ./publish/WebApp.staticwebassets.endpoints.json | grep "Cache-Control"
```

**Test in production**:
```bash
# Fingerprinted - should be immutable
curl -I https://yourapp.com/css/app.c68w6wcsvq.css

# Non-fingerprinted - should be no-cache
curl -I https://yourapp.com/css/app.css
```

## Best Practices

### 1. Always Use Release Configuration

```bash
# ✅ Correct
dotnet publish -c Release

# ❌ Incorrect (no optimization)
dotnet publish
```

### 2. Don't Reference Assets Directly

```razor
<!-- ❌ Wrong: Hard-coded URL -->
<link rel="stylesheet" href="/css/app.css" />

<!-- ✅ Correct: Let framework handle URL -->
<link rel="stylesheet" href="~/css/app.css" />
```

The `~` syntax allows the framework to automatically substitute fingerprinted URLs.

### 3. Verify Assets in CI

Always include verification step in build pipeline to catch issues early.

### 4. Monitor Compression Ratios

Track compression effectiveness over time:
```bash
# Check original vs compressed sizes
find ./publish/wwwroot -name "*.css" ! -name "*.br" ! -name "*.gz" -exec du -b {} + | awk '{sum+=$1} END {print sum}'
find ./publish/wwwroot -name "*.css.br" -exec du -b {} + | awk '{sum+=$1} END {print sum}'
```

### 5. Test Multiple Browsers

Verify compression works in different browsers:
- Chrome (supports br and gzip)
- Firefox (supports br and gzip)
- Safari (supports gzip)
- Edge (supports br and gzip)
- Older browsers (may only support gzip or none)

### 6. Use CDN with Proper Configuration

Configure CDN to:
- Respect `Vary: Content-Encoding` header
- Forward `Accept-Encoding` to origin
- Cache different encoding variants separately

See [CDN Configuration Guide](./cdn-static-assets-configuration.md) for details.

## Performance Monitoring

### Metrics to Track

1. **Compression Ratio**
   - Original size vs compressed size
   - Target: >70% reduction

2. **Cache Hit Rate**
   - Requests served from cache vs origin
   - Target: >95% for static assets

3. **Page Load Time**
   - Total page load with all assets
   - Target: <2 seconds

4. **Asset Transfer Size**
   - Total bytes transferred for assets
   - Target: <500KB for initial page load

### Tools

- **Browser DevTools**: Network tab, Performance tab
- **Azure Application Insights**: Request metrics, dependencies
- **Azure CDN Analytics**: Cache hit ratio, bandwidth
- **Lighthouse**: Performance score, asset optimization

## References

- [ASP.NET Core Static Files](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/static-files)
- [Blazor Static Assets](https://learn.microsoft.com/en-us/aspnet/core/blazor/fundamentals/static-files)
- [Brotli Compression](https://github.com/google/brotli)
- [HTTP Caching](https://developer.mozilla.org/en-US/docs/Web/HTTP/Caching)
- [Cache-Control Directives](https://developer.mozilla.org/en-US/docs/Web/HTTP/Headers/Cache-Control)

---

**Last Updated**: 2025-10-30  
**Related Documents**:
- [CDN and Static Assets Configuration](./cdn-static-assets-configuration.md)
- [Deployment Playbook](./deployment-playbook.md)
