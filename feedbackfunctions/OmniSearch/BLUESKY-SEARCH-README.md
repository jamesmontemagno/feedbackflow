# BlueSky Search Implementation

## Overview
BlueSky search is integrated into the OmniSearch feature, allowing cross-platform content discovery alongside Twitter, Reddit, YouTube, and Hacker News.

## Architecture

### Components
1. **BlueSkyFeedbackFetcher** (`shareddump/Models/BlueSkyFeedback/BlueSkyFeedbackFetcher.cs`)
   - Core search implementation
   - Endpoint: `https://public.api.bsky.app/xrpc/app.bsky.feed.searchPosts`
   - Handles authentication, rate limiting, and token refresh

2. **BlueSkyServiceAdapter** (`shareddump/Services/BlueSkyServiceAdapter.cs`)
   - Implements `IBlueSkyService` interface
   - Wraps `BlueSkyFeedbackFetcher` for DI

3. **OmniSearchService** (`feedbackfunctions/OmniSearch/OmniSearchService.cs`)
   - `SearchBlueSkyAsync()` method aggregates BlueSky results
   - Converts to `OmniSearchResult` format

4. **OmniSearchFunction** (`feedbackfunctions/OmniSearch/OmniSearchFunction.cs`)
   - Azure Function HTTP endpoint
   - Accepts `"bluesky"` in platforms array

## API Details

### BlueSky Search API
- **⚠️ IMPORTANT**: Use `bsky.social` as the base URL, NOT `public.api.bsky.app`
- **Endpoint**: `https://bsky.social/xrpc/app.bsky.feed.searchPosts`
- **Authentication Required**: Must authenticate first to get access token
- **Parameters**:
  - `q` (string): Search query (URL encoded)
  - `limit` (int): Max results per request (max 100)
  - `sort` (string): "latest" or "top"
  - `cursor` (string, optional): Pagination cursor for next page

### Why Direct API Calls Return 403
If you try to call `https://public.api.bsky.app/xrpc/app.bsky.feed.searchPosts` directly, you'll get:
```
HTTP/1.1 403 Forbidden
```

**Solution**: Use `bsky.social` as the base URL instead:
```
https://bsky.social/xrpc/app.bsky.feed.searchPosts
```

The API requires:
1. Valid authentication token in `Authorization: Bearer {token}` header
2. Token obtained via `com.atproto.server.createSession` endpoint
3. Username and app password credentials

Our implementation handles all of this automatically via `BlueSkyFeedbackFetcher`.

### Authentication
BlueSky requires authentication even for search:
- Uses app password authentication
- Tokens auto-refresh on 401 responses
- Rate limiting handled with 429 detection

### Configuration
Required in `local.settings.json` or Azure App Settings:
```json
{
  "BlueSky": {
    "Username": "your-handle.bsky.social",
    "AppPassword": "your-app-password-here"
  }
}
```

**Get App Password:**
1. Go to https://bsky.app/settings/app-passwords
2. Create new app password
3. Copy and save (cannot view again)

## Data Model

### Search Response
```csharp
public class BlueSkySearchResponse
{
    public List<BlueSkySearchPost>? Posts { get; set; }
    public string? Cursor { get; set; } // For pagination
}
```

### Search Post
```csharp
public class BlueSkySearchPost
{
    public string Uri { get; set; }           // at://did:plc:.../app.bsky.feed.post/...
    public string Cid { get; set; }           // Content ID
    public BlueSkySearchAuthor? Author { get; set; }
    public BlueSkySearchRecord? Record { get; set; }
    public int ReplyCount { get; set; }
    public int RepostCount { get; set; }
    public int LikeCount { get; set; }
    public string IndexedAt { get; set; }     // ISO 8601 timestamp
}
```

### OmniSearch Result Mapping
Our implementation correctly maps the BlueSky search response:
- **Id**: Extracted from URI (last segment: `{postId}`)
- **Title**: `@author: {first 100 chars of content}`
- **Snippet**: First 200 chars of content
- **Source**: "BlueSky"
- **Url**: `https://bsky.app/profile/{author}/post/{postId}`
- **PublishedAt**: Post timestamp from `record.createdAt` or `indexedAt`
- **Author**: `@{handle}`
- **EngagementCount**: Currently 0 (engagement data available but not yet implemented)

**Available Engagement Data** (not yet used):
- `replyCount` - Number of replies
- `repostCount` - Number of reposts
- `likeCount` - Number of likes  
- `bookmarkCount` - Number of bookmarks
- `quoteCount` - Number of quote posts

## Error Handling

### Rate Limiting
- Detects 429 status code
- Sets `_hitRateLimit` flag
- Returns empty results gracefully
- Logs warning

### Authentication Failures
- Auto-retries with token refresh on 401
- Falls back to mock service if credentials missing
- Logs configuration errors

### Search Failures
- Catches and logs exceptions
- Returns empty list (doesn't fail entire search)
- Preserves results from other platforms

## Testing

### Direct API Test
Use `bluesky-search-test.http` to test the public API:
```http
GET https://public.api.bsky.app/xrpc/app.bsky.feed.searchPosts?q=dotnet&limit=10&sort=latest
```

### OmniSearch Function Test
```http
POST http://localhost:7071/api/OmniSearch?code=your-key
Content-Type: application/json

{
  "query": "dotnet",
  "platforms": ["bluesky"],
  "maxResults": 10
}
```

### Multi-Platform Test
```http
POST http://localhost:7071/api/OmniSearch?code=your-key
Content-Type: application/json

{
  "query": "dotnet",
  "platforms": ["bluesky", "twitter", "reddit"],
  "maxResults": 20
}
```

## Mock Service
When credentials are not configured:
- Uses `MockBlueSkyService`
- Returns sample data
- Console logs "Using mock data for BlueSky"
- Prevents startup failures

## Known Limitations
1. **Engagement Metrics**: Not currently stored in `BlueSkyFeedbackItem` model
2. **Pagination**: Not implemented (only first page of results)
3. **Sorting**: Only "latest" sort supported (not "top")
4. **Date Filtering**: Client-side filtering after fetch (API doesn't support date range)

## Future Enhancements
- [ ] Add engagement metrics (likes, reposts, replies) to model
- [ ] Implement cursor-based pagination
- [ ] Support "top" sort option
- [ ] Server-side date filtering if API adds support
- [ ] Add result caching
- [ ] Track per-platform usage/quotas

## Troubleshooting

### "Using mock data for BlueSky"
- Missing credentials in configuration
- Check `local.settings.json` or Azure App Settings
- Verify `BlueSky:Username` and `BlueSky:AppPassword` keys

### No Results Returned
- Check rate limiting (429 errors in logs)
- Verify credentials are valid
- Test query with direct API call
- Check network connectivity

### Authentication Errors
- App password may be revoked/expired
- Generate new app password at https://bsky.app/settings/app-passwords
- Update configuration with new password

### Rate Limit Exceeded
- BlueSky has rate limits on search
- Wait before retrying
- Consider implementing backoff/retry logic
- Check if hitting limits due to multiple simultaneous requests
