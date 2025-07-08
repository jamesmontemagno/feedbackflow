# Reports and Sharing in FeedbackFlow

This document outlines how the reporting and sharing functionality works in FeedbackFlow, including both the feedback reports and analysis sharing features.

## Report System Overview

### Components

1. **Report Service**
   - `IReportServiceProvider` - Provider interface that manages service instantiation
   - `IReportService` - Interface defining report operations
   - `ReportServiceProvider` - Service provider that decides between real and mock implementations
   - `ReportService` - Implementation for real API calls
   - `MockReportService` - Implementation for testing/development

The service provider pattern allows for runtime switching between real and mock implementations. Components inject `IReportServiceProvider` and get the appropriate implementation through the `GetService()` method:

```csharp
public class SomeComponent
{
    private readonly IReportService _reportService;

    protected override void OnInitialized()
    {
        _reportService = ReportServiceProvider.GetService();
    }
}
```

2. **Azure Functions**
   - **Report Generation Functions** (`ReportingFunctions`):
     - `GetReport` - Retrieves individual reports by ID
     - `GetUserReports` - Gets reports for the authenticated user
   - **Report Request Functions** (`ReportRequestFunctions`):
     - Global request management (for background processing):     - `AddUserReportRequest` - Adds user-specific report requests
     - `RemoveUserReportRequest` - Removes user-specific report requests
     - `ListUserReportRequests` - Lists user-specific report requests
     - User-specific request management (requires authentication):
       - `AddUserReportRequest` - Adds user-specific report requests
       - `RemoveUserReportRequest` - Removes user-specific report requests  
       - `ListUserReportRequests` - Lists authenticated user's report requests

3. **UI Components**
   - `Reports.razor` - Main reports listing page
   - `Report.razor` - Individual report view

### Configuration

The report system can be configured to use mock data by setting:
```json
{
  "UseMockServices": true
}
```

### User-Specific Report Request Architecture

The system now supports user-specific report requests with a dual-table approach:

#### Table Structure

1. **`reportrequests` Table** (Global requests for background processing):
   - **PartitionKey**: Source type (`reddit`, `github`)
   - **RowKey**: Generated ID based on source and details
   - **SubscriberCount**: Number of users interested in this request
   - Used for background report generation and tracking overall demand

2. **`userreportrequests` Table** (User-specific requests):
   - **PartitionKey**: User ID
   - **RowKey**: Generated ID based on source and details
   - **UserId**: The authenticated user who made the request
   - **Type, Subreddit, Owner, Repo**: Request details
   - Used for filtering reports per user and managing individual subscriptions

#### Request Flow

1. **Adding a Request**:
   - User authenticates and adds a request via `AddUserReportRequest`
   - System validates the request (subreddit/repo exists)
   - User request is stored in `userreportrequests` table
   - Global request is created/updated in `reportrequests` table with incremented subscriber count
   - Background report generation is triggered for new global requests

2. **Removing a Request**:
   - User request is removed from `userreportrequests` table
   - Global request subscriber count is decremented
   - If subscriber count reaches 0, global request is deleted

3. **Filtering Reports**:
   - `GetUserReports` function now uses the authenticated user's ID
   - Only returns reports for sources the user has specifically requested
   - No longer accepts request data in the POST body - uses stored user preferences

#### Cache Optimization

The cache system is still utilized for report retrieval but now works in conjunction with user-specific filtering:
- Reports are cached globally for performance
- User-specific filtering happens at query time using the user's stored requests
- This provides the best balance of performance and personalization

### Authentication

All report-related pages require authentication through the `AuthenticationService`.

## Analysis Sharing System

### Components

1. **History Management**
   - `HistoryService` - Manages analysis history
   - `AnalysisResults.razor` - Displays analysis results with sharing options
   - `History.razor` - Lists and manages saved analyses

2. **Sharing Features**
   - `AnalysisSharingService` - Handles sharing functionality
   - Share states:
     - Private (default)
     - Shared with link
     - Public

### Sharing Workflow

1. **Creating a Share**
   - User clicks share button in `AnalysisResults`
   - System generates unique share ID
   - Content saved to storage with share permissions

2. **Accessing Shared Content**
   - Shared links accessible without authentication
   - Public items discoverable through search
   - Original owner can revoke access

## Integration Points

### Report Generation to Sharing

1. Report Generation:
   ```mermaid
   graph LR
   A[Azure Function] --> B[Report Generation]
   B --> C[Store Report]
   C --> D[Notify Users]
   ```

2. Sharing Flow:
   ```mermaid
   graph LR
   A[Analysis] --> B[Save to History]
   B --> C[Share Analysis]
   C --> D[Generate Link]
   ```

## Data Models

### ReportModel
```csharp
public class ReportModel
{
    public Guid Id { get; set; }
    public string Source { get; set; }
    public string SubSource { get; set; }
    public DateTimeOffset GeneratedAt { get; set; }
    public string HtmlContent { get; set; }
    public int ThreadCount { get; set; }
    public int CommentCount { get; set; }
    public DateTimeOffset CutoffDate { get; set; }
}
```

### AnalysisHistoryItem
```csharp
public record AnalysisHistoryItem
{
    public string Id { get; init; }
    public DateTime Timestamp { get; init; }
    public string Summary { get; init; }
    public string FullAnalysis { get; init; }
    public string SourceType { get; init; }
    public string? UserInput { get; init; }
    public bool IsShared { get; init; }
    public string? SharedId { get; init; }
    public DateTime? SharedDate { get; init; }
}
```

## Best Practices

1. **Error Handling**
   - Always provide user feedback for operations
   - Gracefully handle loading states
   - Implement proper error boundaries

2. **Performance**
   - Use mock services for development
   - Implement caching where appropriate
   - Lazy load content when possible

3. **Security**
   - Validate all user input
   - Implement proper authentication checks
   - Use secure sharing links

4. **UI/UX**
   - Show loading states during operations
   - Provide clear feedback for actions
   - Maintain consistent styling

## Future Improvements

1. **Report System**
   - Support for additional data sources
   - Advanced filtering options
   - Batch report generation

2. **Sharing System**
   - Granular permissions control
   - Team sharing capabilities
   - Integration with external platforms

3. **Analytics**
   - Usage tracking for shared content
   - Report generation metrics
   - Performance monitoring
