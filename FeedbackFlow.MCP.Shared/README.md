# FeedbackFlow MCP Shared Library

This shared library consolidates common authentication and tool logic for both local and remote FeedbackFlow MCP servers.

## Architecture

### Authentication Providers

- **LocalAuthenticationProvider**: Uses `FEEDBACKFLOW_API_KEY` environment variable for local development
- **RemoteAuthenticationProvider**: Uses Bearer token from HTTP Authorization header for remote deployment

### Shared Tools

- **FeedbackFlowToolsShared**: Contains all MCP tool implementations that work with both authentication providers

## Usage

### Local MCP Server (FeedbackFlowLocalMCP)
```csharp
builder.Services.AddScoped<IAuthenticationProvider, LocalAuthenticationProvider>();
```

### Remote MCP Server (FeedbackFlowMCP)  
```csharp
builder.Services.AddScoped<IAuthenticationProvider, RemoteAuthenticationProvider>();
```

## Benefits

1. **No Code Duplication**: Single source of truth for all tool implementations
2. **Consistent Authentication**: Transparent handling of both API key and Bearer token scenarios
3. **Easy Maintenance**: Changes to tool logic only need to be made in one place
4. **Type Safety**: Shared interfaces ensure consistent behavior across implementations

## Tools Available

- `AutoAnalyzeFeedback`: Analyze feedback from various sources (GitHub, YouTube, Reddit, etc.)
- `RedditReport`: Generate Reddit subreddit analysis report
- `GitHubIssuesReport`: Generate GitHub repository issues analysis report  
- `RedditReportSummary`: Generate Reddit subreddit summary report