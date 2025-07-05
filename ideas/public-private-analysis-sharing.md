# Public/Private Analysis Sharing Feature

## Overview
Add functionality to allow users to mark shared analyses as either public or private. Private analyses (default) can only be accessed by the owner, while public analyses can be accessed by anyone with the link.

## Backend Changes

### 1. Database Schema Updates

#### SharedAnalysisEntity Model
- Add `IsPublic` boolean property (default: false)
- Add `PublicSharedDate` DateTime property (when made public)
- ~~Add `PublicAccessToken` string property~~ **REMOVED - Not needed**

```csharp
public class SharedAnalysisEntity : ITableEntity
{
    // ... existing properties ...
    
    /// <summary>
    /// Whether this analysis is publicly accessible
    /// </summary>
    public bool IsPublic { get; set; } = false;
    
    /// <summary>
    /// When this analysis was made public (if applicable)
    /// </summary>
    public DateTime? PublicSharedDate { get; set; }
}
```

#### Why PublicAccessToken Was Removed
The `PublicAccessToken` adds complexity without meaningful security benefits:

**Potential Use Cases Considered:**
1. **"Unlisted" vs "Public"** - Like YouTube's unlisted videos, where you need the exact link
2. **Revocation Control** - Ability to invalidate shared links without changing public status  
3. **Analytics Tracking** - Track which specific share attempts are being used
4. **Anti-Scraping** - Make it harder to enumerate all public analyses

**Why It's Not Worth It:**
- **Security Theater**: If it's public, the token doesn't add real security
- **Complexity**: Two-tier public system (public + token) confuses users
- **URL Ugliness**: `/analysis/abc123?token=xyz789` vs clean `/analysis/abc123`
- **Caching Issues**: Tokens complicate CDN and browser caching
- **Enumeration**: If someone wants to scrape, they'll find other ways

**Better Alternatives:**
- Use GUIDs for analysis IDs (already hard to enumerate)
- Implement rate limiting and monitoring for abuse
- Add proper "unlisted" as a separate visibility level if needed

### 2. Azure Functions Updates

#### SaveSharedAnalysis Function
- Add optional `isPublic` parameter in request body
- Set `PublicSharedDate` if making public

#### GetSharedAnalysis Function
- **BREAKING CHANGE**: Currently anonymous, needs authentication logic
- If analysis is private: require authentication and ownership verification
- If analysis is public: allow anonymous access
- Simple public check: `if (entity.IsPublic || userOwnsAnalysis) { return analysis; }`

#### New Functions to Add
1. **UpdateAnalysisVisibility** - Allow users to change public/private status

```csharp
[Function("UpdateAnalysisVisibility")]
[Authorize]
public async Task<HttpResponseData> UpdateAnalysisVisibility(
    [HttpTrigger(AuthorizationLevel.Function, "patch", Route = "UpdateAnalysisVisibility/{id}")] HttpRequestData req,
    string id)
{
    // Verify ownership, update IsPublic flag, set/clear PublicSharedDate
}
```

### 3. Security Considerations
- Implement rate limiting on public analysis access
- Consider adding view counters for public analyses
- Optional: Implement reporting mechanism for inappropriate public content
- Cache public analyses more aggressively than private ones
- Use GUIDs for analysis IDs to prevent enumeration attacks

## Frontend Changes

### 1. UI/UX Updates

#### Analysis Results Component (`AnalysisResults.razor`)
- Add privacy toggle (public/private) to sharing options
- Show current visibility status
- Add warning when making analysis public
- Update share button to show different states:
  - "Save Privately" (default)
  - "Save & Make Public" 
  - "Save & Keep Private"

#### New Components Needed
1. **VisibilityToggle.razor** - Toggle component for public/private
2. **ShareLinkGenerator.razor** - Generate shareable public links

#### Shared History Page Updates
- Add visibility indicator (lock icon for private, globe for public)
- Add quick action to change visibility directly in the history list
- Show public share link for public analyses with copy-to-clipboard
- Add filter options (show all, private only, public only)
- Integrate privacy controls and share link management into existing UI

### 2. Service Layer Updates

#### ISharedHistoryService Interface
```csharp
// Add new methods
Task<string> ShareAnalysisAsync(AnalysisData analysis, bool isPublic = false);
Task<bool> UpdateAnalysisVisibilityAsync(string analysisId, bool isPublic);
Task<string?> GetPublicShareLinkAsync(string analysisId);
```

#### SharedHistoryService Implementation
- Update `ShareAnalysisAsync` to accept `isPublic` parameter
- Add methods for visibility management
- Handle public link generation
- Update caching strategy (separate cache for public vs private)

#### MockSharedHistoryService
- Add corresponding mock implementations
- Generate realistic mock public analyses

### 3. URL Structure Changes

#### New Routes Needed
- `/analysis/{id}` - Analysis viewer (handles both public and private analyses)

#### Existing Route Updates
- Remove old `/shared/{id}` and `/shared-analysis/{id}` routes
- Update navigation to use single `/analysis/{id}` route

### 4. User Experience Enhancements

#### Privacy Controls
- Clear labeling of public vs private
- Confirmation dialog when making analysis public
- Easy way to revert from public to private

#### Share Link Management
- Copy-to-clipboard functionality integrated into SharedHistory page
- Social media sharing buttons for public analyses

## Implementation Phases

### Phase 1: Core Functionality
1. Update database schema
2. Modify SaveSharedAnalysis and GetSharedAnalysis functions
3. Add basic privacy toggle in UI
4. Update shared history to show visibility status

### Phase 2: Enhanced Sharing
1. Add UpdateAnalysisVisibility function
2. Implement public share link generation
3. Add share link management UI to SharedHistory page
4. Implement confirmation dialogs and warnings


## Security & Privacy Considerations

### Data Protection
- Ensure public analyses don't contain sensitive information
- Add content warnings/disclaimers
- Implement user consent for making analyses public
- Right to be forgotten (delete all public analyses)

### Access Control
- Rate limiting on public analysis access
- Prevent enumeration attacks on analysis IDs (use GUIDs)
- Audit logging for public analysis access
- Monitor for abuse patterns and scraping attempts

### Content Moderation
- User education about public sharing risks
- Terms of service updates
