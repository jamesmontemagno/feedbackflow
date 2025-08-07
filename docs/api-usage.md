# FeedbackFlow API Usage Guide

This document provides comprehensive guidance for using the FeedbackFlow REST API, including authentication, available endpoints, usage limits, and examples.

## Overview

The FeedbackFlow API allows Pro+ subscribers and above to programmatically access feedback analysis and reporting functionality. All API calls require authentication via API keys and are subject to monthly usage limits.

## Authentication

### API Key Requirements

- **Availability**: API keys are available for Pro+ subscribers, SuperUser, and Admin accounts only
- **Generation**: Generate your API key through the Account Settings page in the FeedbackFlow web interface
- **Approval**: All API keys are disabled by default and require administrator approval for security purposes
- **Limitation**: Only one API key is allowed per user account

### API Key Usage

API keys can be provided in two ways:

#### Option 1: HTTP Header (Recommended)
```bash
curl -H "x-api-key: ff_YOUR_API_KEY" \
  "https://api.feedbackflow.app/api/AutoAnalyze?url=https://github.com/dotnet/maui/issues/123"
```

#### Option 2: Query Parameter
```bash
curl "https://api.feedbackflow.app/api/AutoAnalyze?apikey=ff_YOUR_API_KEY&url=https://github.com/dotnet/maui/issues/123"
```

## Usage Limits

API usage is tracked monthly and resets automatically at the beginning of each month.

### Limits by Account Tier

| Account Tier | Monthly API Calls | Cost per AutoAnalyze | Cost per Report |
|-------------|-------------------|---------------------|----------------|
| Free        | Not Available     | N/A                 | N/A            |
| Pro         | Not Available     | N/A                 | N/A            |
| **Pro+**    | **100 calls**     | **1 point**         | **2 points**   |
| SuperUser   | 1000 calls        | 1 point             | 2 points       |
| Admin       | 1000 calls        | 1 point             | 2 points       |

### Usage Calculation Examples

- **10 AutoAnalyze calls + 20 Reports** = 10×1 + 20×2 = **50 points used**
- **50 AutoAnalyze calls + 25 Reports** = 50×1 + 25×2 = **100 points used** (Pro+ limit reached)

## Available Endpoints

### 1. AutoAnalyze

Analyzes feedback from various platforms and returns AI-generated insights.

**Endpoint:** `GET /api/AutoAnalyze`

**Usage Cost:** 1 API point

**Response Types:**
- **Type 0 (Analysis only)**: Default behavior. Returns AI-generated analysis as markdown text. Best for applications that only need insights and summaries.
- **Type 1 (Comments only)**: Returns raw extracted comments without analysis. Useful for applications that want to perform their own processing or display raw feedback.
- **Type 2 (Both)**: Returns both raw comments and AI analysis in JSON format. Best for comprehensive applications that need both the source data and insights.

**Supported Platforms:**
- GitHub Issues, Pull Requests, and Discussions
- YouTube Videos
- Reddit Posts
- Hacker News Stories
- Twitter/X Posts
- BlueSky Posts
- Developer Blogs

#### Parameters

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `url` | string | Yes | The URL to analyze |
| `maxComments` | int | No | Maximum number of comments to analyze (default: platform-specific) |
| `type` | int | No | Response type: 0 = analysis only (default), 1 = comments only, 2 = both comments and analysis |

#### Example Requests

```bash
# GitHub Issue Analysis (default - analysis only)
curl -H "x-api-key: ff_YOUR_API_KEY" \
  "https://api.feedbackflow.app/api/AutoAnalyze?url=https://github.com/dotnet/maui/issues/123&maxComments=50"

# YouTube Video Analysis - comments only
curl -H "x-api-key: ff_YOUR_API_KEY" \
  "https://api.feedbackflow.app/api/AutoAnalyze?url=https://www.youtube.com/watch?v=VIDEO_ID&type=1"

# Reddit Post Analysis - both comments and analysis
curl -H "x-api-key: ff_YOUR_API_KEY" \
  "https://api.feedbackflow.app/api/AutoAnalyze?url=https://www.reddit.com/r/programming/comments/POST_ID/&type=2"

# Using query parameter authentication with analysis only
curl "https://api.feedbackflow.app/api/AutoAnalyze?apikey=ff_YOUR_API_KEY&url=https://github.com/dotnet/maui/issues/123&type=0"
```

#### Response Format

The response format depends on the `type` parameter:

**Type 0 (Analysis only - default):**
```json
"## Analysis Title\n\nDetailed AI-generated analysis of the feedback..."
```

**Type 1 (Comments only):**
```json
{
  "comments": "Raw comments data extracted from the platform..."
}
```

**Type 2 (Both comments and analysis):**
```json
{
  "comments": "Raw comments data extracted from the platform...",
  "analysis": "## Analysis Title\n\nDetailed AI-generated analysis..."
}
```

**Legacy format (for backward compatibility):**
When `type=0` or not specified, the response is returned as plain text markdown for backward compatibility with existing integrations.

### 2. RedditReport

Generates comprehensive reports for Reddit subreddits over a specified time period.

**Endpoint:** `GET /api/RedditReport`

**Usage Cost:** 2 API points

#### Parameters

| Parameter | Type | Required | Default | Description |
|-----------|------|----------|---------|-------------|
| `subreddit` | string | Yes | - | Subreddit name (without r/) |
| `days` | int | No | 7 | Number of days to analyze |
| `limit` | int | No | 25 | Maximum posts to analyze |
| `sort` | string | No | "hot" | Sort method: hot, new, top, rising |
| `force` | bool | No | false | Force new report generation |

#### Example Requests

```bash
# Basic subreddit report
curl -H "x-api-key: ff_YOUR_API_KEY" \
  "https://api.feedbackflow.app/api/RedditReport?subreddit=dotnet"

# Detailed report with custom parameters
curl -H "x-api-key: ff_YOUR_API_KEY" \
  "https://api.feedbackflow.app/api/RedditReport?subreddit=programming&days=30&limit=50&sort=top&force=true"
```

#### Response Format

```json
{
  "id": "report-id",
  "subreddit": "dotnet",
  "generatedAt": "2024-01-15T10:30:00Z",
  "timeframe": "Last 7 days",
  "totalPosts": 25,
  "totalComments": 150,
  "summary": "Key trends and insights...",
  "topPosts": [
    {
      "title": "Post title",
      "url": "https://reddit.com/...",
      "score": 45,
      "commentCount": 12
    }
  ],
  "insights": "AI-generated insights about the subreddit activity..."
}
```

### 3. GitHubIssuesReport

Generates reports for GitHub repository issues and discussions.

**Endpoint:** `GET /api/GitHubIssuesReport`

**Usage Cost:** 2 API points

#### Parameters

| Parameter | Type | Required | Default | Description |
|-----------|------|----------|---------|-------------|
| `repo` | string | Yes | - | Repository in format "owner/repo" |
| `days` | int | No | 7 | Number of days to analyze |
| `force` | bool | No | false | Force new report generation |

#### Example Requests

```bash
# Basic repository report
curl -H "x-api-key: ff_YOUR_API_KEY" \
  "https://api.feedbackflow.app/api/GitHubIssuesReport?repo=microsoft/vscode"

# Force new report for longer timeframe
curl -H "x-api-key: ff_YOUR_API_KEY" \
  "https://api.feedbackflow.app/api/GitHubIssuesReport?repo=dotnet/maui&days=30&force=true"
```

#### Response Format

```json
{
  "id": "report-id",
  "repository": "microsoft/vscode",
  "generatedAt": "2024-01-15T10:30:00Z",
  "timeframe": "Last 7 days",
  "totalIssues": 45,
  "openIssues": 38,
  "closedIssues": 7,
  "totalComments": 120,
  "summary": "Repository activity summary...",
  "topIssues": [
    {
      "title": "Issue title",
      "url": "https://github.com/microsoft/vscode/issues/123",
      "state": "open",
      "commentCount": 15
    }
  ],
  "insights": "AI-generated insights about repository activity..."
}
```

### 4. RedditReportSummary

Generates condensed summary reports for Reddit subreddits.

**Endpoint:** `GET /api/RedditReportSummary`

**Usage Cost:** 2 API points

#### Parameters

| Parameter | Type | Required | Default | Description |
|-----------|------|----------|---------|-------------|
| `subreddit` | string | Yes | - | Subreddit name (without r/) |
| `force` | bool | No | false | Force new report generation |

#### Example Requests

```bash
# Basic summary report
curl -H "x-api-key: ff_YOUR_API_KEY" \
  "https://api.feedbackflow.app/api/RedditReportSummary?subreddit=dotnet"

# Force new summary
curl -H "x-api-key: ff_YOUR_API_KEY" \
  "https://api.feedbackflow.app/api/RedditReportSummary?subreddit=programming&force=true"
```

#### Response Format

```json
{
  "subreddit": "dotnet",
  "generatedAt": "2024-01-15T10:30:00Z",
  "timeframe": "Recent activity",
  "postCount": 25,
  "engagementScore": 8.5,
  "summary": "Condensed overview of subreddit activity...",
  "keyTopics": ["C#", ".NET 9", "Performance", "WebAPI"],
  "sentiment": "Positive"
}
```

## Error Handling

### Common Error Responses

#### 401 Unauthorized
```json
{
  "error": "API key is required. Provide it in 'x-api-key' header or 'apikey' query parameter."
}
```

#### 401 Invalid API Key
```json
{
  "error": "Invalid or disabled API key. Contact admin to enable your API key."
}
```

#### 429 Usage Limit Exceeded
```json
{
  "isWithinLimit": false,
  "usageType": "ApiCall",
  "currentUsage": 100,
  "limit": 100,
  "currentTier": "ProPlus",
  "errorCode": "USAGE_LIMIT_EXCEEDED",
  "message": "API usage limit exceeded for this month"
}
```

#### 400 Bad Request
```json
{
  "error": "Invalid URL format or missing required parameters"
}
```

#### 500 Internal Server Error
```json
{
  "error": "Internal server error occurred while processing the request"
}
```

## Best Practices

### 1. Error Handling
Always implement proper error handling in your applications:

```javascript
async function analyzeContent(url, apiKey, responseType = 0) {
  try {
    const response = await fetch(`https://api.feedbackflow.app/api/AutoAnalyze?url=${encodeURIComponent(url)}&type=${responseType}`, {
      headers: {
        'x-api-key': apiKey
      }
    });
    
    if (!response.ok) {
      const error = await response.text();
      throw new Error(`API Error: ${response.status} - ${error}`);
    }
    
    // Handle different response types
    if (responseType === 0) {
      return await response.text(); // Analysis as markdown text
    } else {
      return await response.json(); // Comments and/or analysis as JSON
    }
  } catch (error) {
    console.error('Analysis failed:', error);
    throw error;
  }
}
```

### 2. Rate Limiting
Monitor your API usage to avoid hitting monthly limits:

```javascript
// Check usage before making API calls
function checkUsageLimit(currentUsage, limit) {
  const remainingCalls = limit - currentUsage;
  if (remainingCalls < 10) {
    console.warn(`Warning: Only ${remainingCalls} API calls remaining this month`);
  }
  return remainingCalls > 0;
}
```

### 3. Caching
Consider caching results to minimize API usage:

```javascript
const cache = new Map();

async function getCachedAnalysis(url, apiKey, responseType = 0, maxAgeMinutes = 60) {
  const cacheKey = `${url}-${responseType}`;
  const cached = cache.get(cacheKey);
  
  if (cached && Date.now() - cached.timestamp < maxAgeMinutes * 60 * 1000) {
    return cached.data;
  }
  
  const result = await analyzeContent(url, apiKey, responseType);
  cache.set(cacheKey, { data: result, timestamp: Date.now() });
  
  return result;
}
```

### 4. Batch Processing
For multiple URLs, process them sequentially to avoid overwhelming the service:

```javascript
async function batchAnalyze(urls, apiKey, responseType = 0, delayMs = 1000) {
  const results = [];
  
  for (const url of urls) {
    try {
      const result = await analyzeContent(url, apiKey, responseType);
      results.push({ url, result, success: true });
    } catch (error) {
      results.push({ url, error: error.message, success: false });
    }
    
    // Add delay between requests
    if (delayMs > 0) {
      await new Promise(resolve => setTimeout(resolve, delayMs));
    }
  }
  
  return results;
}
```

## Getting Started

### Step 1: Account Setup
1. Ensure you have a Pro+ or higher account tier
2. Navigate to Account Settings in the FeedbackFlow web interface
3. Locate the "API Key Management" section

### Step 2: Generate API Key
1. Click "Generate API Key" to create your key
2. Copy and securely store your API key (it begins with `ff_`)
3. Contact an administrator to enable your API key for use

### Step 3: Test Your Setup
```bash
# Test with a simple GitHub issue analysis (default - analysis only)
curl -H "x-api-key: ff_YOUR_ACTUAL_API_KEY" \
  "https://api.feedbackflow.app/api/AutoAnalyze?url=https://github.com/dotnet/aspnetcore/issues/1"

# Test with comments only
curl -H "x-api-key: ff_YOUR_ACTUAL_API_KEY" \
  "https://api.feedbackflow.app/api/AutoAnalyze?url=https://github.com/dotnet/aspnetcore/issues/1&type=1"

# Test with both comments and analysis
curl -H "x-api-key: ff_YOUR_ACTUAL_API_KEY" \
  "https://api.feedbackflow.app/api/AutoAnalyze?url=https://github.com/dotnet/aspnetcore/issues/1&type=2"
```

### Step 4: Monitor Usage
- Check your usage dashboard in Account Settings
- API usage resets monthly on the first day of each month
- Plan your usage to stay within your tier's limits

## Support

### Getting Help
- **Documentation Issues**: Check this guide and the web interface help sections
- **API Key Problems**: Contact your administrator for key enablement
- **Technical Issues**: Use the support channels provided in your account dashboard
- **Usage Questions**: Monitor your usage dashboard and plan accordingly

### API Key Management
- **Disabled Keys**: New keys require admin approval before they can be used
- **Key Rotation**: Delete and regenerate your key if needed for security
- **Lost Keys**: API keys cannot be recovered; generate a new one if lost

---

*This documentation covers FeedbackFlow API version as of 2024. API endpoints, parameters, and limits are subject to change. Always refer to the latest documentation and your account dashboard for current information.*