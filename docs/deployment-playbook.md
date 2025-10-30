# FeedbackFlow Deployment Playbook

This playbook provides step-by-step instructions for deploying FeedbackFlow to production and staging environments with optimized static asset delivery.

## Table of Contents

1. [Pre-Deployment Checklist](#pre-deployment-checklist)
2. [Deployment Environments](#deployment-environments)
3. [Deployment Procedures](#deployment-procedures)
4. [Post-Deployment Verification](#post-deployment-verification)
5. [Rollback Procedures](#rollback-procedures)
6. [Troubleshooting](#troubleshooting)

## Pre-Deployment Checklist

Before deploying, ensure the following:

### Code Quality
- [ ] All tests passing (`dotnet test FeedbackFlow.slnx --configuration Release`)
- [ ] Code review completed and approved
- [ ] Static assets optimization verified (CI validation passes)
- [ ] No security vulnerabilities in dependencies
- [ ] Version number updated (if applicable)

### Configuration
- [ ] Environment-specific settings reviewed in `appsettings.Production.json`
- [ ] API keys and secrets stored in Azure Key Vault
- [ ] Database connection strings verified
- [ ] Azure Storage connection strings updated

### Static Assets
- [ ] Fingerprinted assets generated (verified in publish output)
- [ ] Brotli (.br) and Gzip (.gz) compression working
- [ ] `WebApp.staticwebassets.endpoints.json` manifest present
- [ ] Cache-Control headers configured correctly

### Infrastructure
- [ ] Azure resources provisioned and healthy
- [ ] CDN/Front Door configured (if applicable)
- [ ] SSL certificates valid and not expiring soon
- [ ] Monitoring and alerting configured

## Deployment Environments

### Staging Environment

**Purpose**: Pre-production testing and validation

**URL**: https://staging.feedbackflow.app

**Azure Resources**:
- App Service: `feedbackwebapp20250414225345` (staging slot)
- Functions: `feedbackfunctions20250414121421` (staging slot)

**Deployment Method**: Automatic via GitHub Actions on PR
- Workflow: `.github/workflows/deploy-staging.yml`
- Trigger: Push to PR branches

### Production Environment

**Purpose**: Live user-facing application

**URL**: https://feedbackflow.app (or https://www.feedbackflow.app)

**Azure Resources**:
- App Service: `feedbackwebapp20250414225345` (production slot)
- Functions: `feedbackfunctions20250414121421` (production slot)
- CDN: (Optional, see CDN configuration)

**Deployment Method**: Automatic via GitHub Actions on merge to main
- Workflow: `.github/workflows/main_feedbackwebapp20250414225345.yml`
- Trigger: Push to `main` branch

## Deployment Procedures

### Automatic Deployment (Recommended)

#### Staging Deployment

1. **Create or update Pull Request**
   ```bash
   git checkout -b feature/my-feature
   # Make changes
   git add .
   git commit -m "Add new feature"
   git push origin feature/my-feature
   ```

2. **Create PR on GitHub**
   - GitHub Actions automatically triggers staging deployment
   - Wait for CI/CD pipeline to complete
   - Check PR comments for staging URL

3. **Verify Staging Deployment**
   - Visit staging URL: https://staging.feedbackflow.app
   - Test new features and changes
   - Verify static assets load correctly
   - Check browser console for errors
   - Test in multiple browsers (Chrome, Firefox, Safari, Edge)

4. **Request Review**
   - Tag reviewers on PR
   - Address feedback and push updates
   - Staging automatically redeploys on new commits

#### Production Deployment

1. **Merge PR to Main**
   ```bash
   # After PR approval
   git checkout main
   git pull origin main
   git merge feature/my-feature
   git push origin main
   ```
   
   Or use GitHub UI to merge PR.

2. **Monitor Deployment**
   - Watch GitHub Actions workflow: `.github/workflows/main_feedbackwebapp20250414225345.yml`
   - Monitor build logs for any errors
   - Verify publish artifact includes static assets
   - Check deployment succeeds to Azure

3. **Verify Static Assets**
   The CI pipeline automatically verifies:
   - Fingerprinted assets present (e.g., `css/app.*.css`)
   - Brotli compressed files (`.br`)
   - Gzip compressed files (`.gz`)
   - Static assets manifest (`WebApp.staticwebassets.endpoints.json`)
   - Correct cache headers in manifest

4. **Post-Deployment Steps**
   - Wait 2-3 minutes for app service to warm up
   - Verify production URL: https://feedbackflow.app
   - Check CDN cache (if configured)
   - Monitor Application Insights for errors

### Manual Deployment (Emergency Only)

If automated deployment fails, use manual deployment:

#### Web App Manual Deploy

```bash
# Ensure you're on the correct branch
git checkout main
git pull origin main

# Build and publish
dotnet publish ./feedbackwebapp/WebApp.csproj -c Release -o ./publish

# Verify static assets
echo "Verifying static assets..."
find ./publish/wwwroot -name "*.br" -o -name "*.gz" | head -10

# Deploy using Azure CLI
az login

az webapp deployment source config-zip \
  --resource-group feedbackflow-rg \
  --name feedbackwebapp20250414225345 \
  --src ./publish.zip
```

#### Azure Functions Manual Deploy

```bash
# Build and publish functions
dotnet publish ./feedbackfunctions/Functions.csproj -c Release -o ./publish-functions

# Deploy
cd ./publish-functions
func azure functionapp publish feedbackfunctions20250414121421
```

## Post-Deployment Verification

### Automated Checks

The following checks run automatically in CI:

1. **Static Assets Verification**
   - Fingerprinted CSS files: `css/*.*.css`
   - Fingerprinted JS files: `js/*.*.js`
   - Brotli compressed files: `*.br`
   - Gzip compressed files: `*.gz`
   - Static assets manifest exists
   - Cache headers configured correctly

2. **Build Verification**
   - Solution builds without errors
   - All tests pass
   - No compiler warnings (critical level)

### Manual Verification

After deployment, manually verify:

#### 1. Application Health

```bash
# Check web app health
curl -I https://feedbackflow.app/

# Expected: HTTP 200 OK
```

#### 2. Static Assets Loading

Open browser DevTools (F12) and verify:

- **Network Tab**:
  - CSS files loaded from fingerprinted URLs
  - JS files loaded from fingerprinted URLs
  - Files served with correct `Content-Encoding` (br or gzip)
  - Cache-Control headers present
  - No 404 errors for assets

- **Console Tab**:
  - No JavaScript errors
  - No failed resource loads
  - Blazor initialized successfully

#### 3. Compression Verification

Test Brotli compression:
```bash
curl -H "Accept-Encoding: br, gzip" \
  -I https://feedbackflow.app/css/app.css

# Look for:
# Content-Encoding: br
# Cache-Control: no-cache
# Vary: Content-Encoding
```

Test fingerprinted asset:
```bash
curl -I https://feedbackflow.app/css/app.c68w6wcsvq.css

# Look for:
# Cache-Control: max-age=31536000, immutable
```

#### 4. Functional Testing

- [ ] Home page loads correctly
- [ ] User can log in/authenticate
- [ ] GitHub feedback analysis works
- [ ] YouTube feedback collection works
- [ ] Reports generate correctly
- [ ] Admin dashboard accessible
- [ ] Dark mode toggle works
- [ ] Mobile responsive layout correct

#### 5. Performance Verification

Use browser DevTools Performance/Lighthouse:

- [ ] **Performance Score**: >90
- [ ] **First Contentful Paint**: <1.5s
- [ ] **Largest Contentful Paint**: <2.5s
- [ ] **Total Page Size**: <1MB (gzipped)
- [ ] **Static Assets Cached**: Cache hit ratio >90%

#### 6. CDN Verification (if configured)

```bash
# Check CDN response
curl -I https://your-cdn-endpoint.azurefd.net/

# Verify cache status
# Look for: X-Cache: HIT or X-Azure-Ref headers
```

#### 7. Monitor Application Insights

Check Azure Application Insights for:
- No error spike after deployment
- Response times normal (<500ms p95)
- Dependency calls successful
- No failed requests

### Verification Checklist

Use this checklist after each deployment:

```markdown
## Deployment Verification - [Date] [Environment]

### Deployment Info
- [ ] Commit SHA: _______
- [ ] Deployment time: _______
- [ ] Deployed by: _______

### Automated Checks
- [ ] CI build passed
- [ ] Unit tests passed
- [ ] Static assets verified
- [ ] Deployment succeeded

### Manual Checks
- [ ] Application loads successfully
- [ ] Static assets loading correctly
- [ ] Compression working (br/gzip)
- [ ] Cache headers correct
- [ ] No console errors
- [ ] Authentication working
- [ ] Core features functional
- [ ] Performance acceptable
- [ ] Mobile responsive

### Monitoring
- [ ] Application Insights shows no errors
- [ ] Response times normal
- [ ] No failed dependencies

### Sign-off
Verified by: _______ at [Time]
```

## Rollback Procedures

### Web App Rollback

#### Option 1: Swap Slots (Recommended)

If using deployment slots:

```bash
# Swap staging back to production
az webapp deployment slot swap \
  --resource-group feedbackflow-rg \
  --name feedbackwebapp20250414225345 \
  --slot staging \
  --target-slot production
```

#### Option 2: Redeploy Previous Version

```bash
# Find previous successful deployment
git log --oneline

# Checkout previous commit
git checkout <previous-commit-sha>

# Deploy manually (see Manual Deployment section)
dotnet publish ./feedbackwebapp/WebApp.csproj -c Release -o ./publish
# ... deploy to Azure
```

#### Option 3: Use Azure Portal

1. Open Azure Portal
2. Navigate to App Service: `feedbackwebapp20250414225345`
3. Go to "Deployment Center"
4. Click "Redeploy" on previous successful deployment
5. Confirm redeployment

### Purge CDN Cache After Rollback

If using CDN, purge cache after rollback:

```bash
# Azure CDN
az cdn endpoint purge \
  --resource-group feedbackflow-cdn-rg \
  --profile-name feedbackflow-cdn \
  --name feedbackflow \
  --content-paths "/*"

# Azure Front Door
az afd endpoint purge \
  --resource-group feedbackflow-cdn-rg \
  --profile-name feedbackflow-frontdoor \
  --endpoint-name feedbackflow-endpoint \
  --content-paths "/*"
```

### Emergency Rollback Checklist

```markdown
## Emergency Rollback - [Date] [Environment]

### Issue Info
- [ ] Issue description: _______
- [ ] Severity: Critical / High / Medium
- [ ] Impact: _______
- [ ] Detected by: _______

### Rollback Actions
- [ ] Notify team of rollback
- [ ] Identify last known good version
- [ ] Perform rollback (method: _______)
- [ ] Verify application working
- [ ] Purge CDN cache (if applicable)
- [ ] Update status page (if applicable)

### Verification
- [ ] Application functioning normally
- [ ] No errors in logs
- [ ] Users able to access site
- [ ] Core functionality working

### Post-Rollback
- [ ] Document root cause: _______
- [ ] Create issue to fix: _______
- [ ] Schedule fix deployment: _______

### Sign-off
Rollback performed by: _______ at [Time]
Verified by: _______ at [Time]
```

## Troubleshooting

### Issue: Static Assets Not Loading

**Symptoms**: 404 errors for CSS/JS files, unstyled pages

**Diagnosis**:
```bash
# Check publish output
ls -la ./publish/wwwroot/css/
ls -la ./publish/wwwroot/js/

# Look for fingerprinted files
find ./publish/wwwroot -name "*.*.css" -o -name "*.*.js"
```

**Solution**:
1. Verify `MapStaticAssets()` is called in Program.cs
2. Ensure publish command uses `-c Release`
3. Check that `WebApp.staticwebassets.endpoints.json` exists in publish output
4. Redeploy application

### Issue: Assets Not Compressed

**Symptoms**: Large file sizes, no Content-Encoding header

**Diagnosis**:
```bash
# Check for compressed files
find ./publish/wwwroot -name "*.br" -o -name "*.gz"

# Test compression
curl -H "Accept-Encoding: br, gzip" -I https://feedbackflow.app/css/app.css
```

**Solution**:
1. Verify `.br` and `.gz` files exist in publish output
2. Check Accept-Encoding header in request
3. Verify CDN is not stripping compression headers
4. Check static assets middleware is configured

### Issue: Cache Not Working

**Symptoms**: High origin request rate, slow load times

**Diagnosis**:
```bash
# Check cache headers
curl -I https://feedbackflow.app/css/app.c68w6wcsvq.css

# Should show: Cache-Control: max-age=31536000, immutable
```

**Solution**:
1. Verify fingerprinted URLs in HTML source
2. Check CDN caching rules
3. Ensure Cache-Control headers present
4. Verify browser cache not disabled in DevTools

### Issue: Wrong Asset Version Served

**Symptoms**: Old CSS/JS being served after deployment

**Diagnosis**:
- Check HTML source for fingerprint hashes
- Verify new deployment successful
- Check CDN cache status

**Solution**:
1. Purge CDN cache
2. Hard refresh browser (Ctrl+Shift+R)
3. Verify HTML references updated fingerprints
4. Check deployment completed successfully

### Issue: CORS Errors

**Symptoms**: Console errors about CORS when loading assets

**Solution**:
1. Verify CDN origin header matches app service hostname
2. Check `--origin-host-header` in CDN configuration
3. Ensure HTTPS used for all requests
4. Verify custom domain configuration

## Emergency Contacts

### On-Call Rotation
- Primary: [Name] - [Contact]
- Secondary: [Name] - [Contact]

### Escalation Path
1. On-call engineer
2. Team lead
3. Engineering manager
4. CTO

### External Support
- Azure Support: Portal or 1-800-XXX-XXXX
- GitHub Actions Support: https://support.github.com

## Related Documentation

- [CDN and Static Assets Configuration](./cdn-static-assets-configuration.md)
- [Production Storage Configuration](./production-storage-configuration.md)
- [Authentication Setup](./centralized-authentication.md)
- [API Usage Documentation](./api-usage.md)

---

**Last Updated**: 2025-10-30  
**Owner**: DevOps Team  
**Review Frequency**: Quarterly
