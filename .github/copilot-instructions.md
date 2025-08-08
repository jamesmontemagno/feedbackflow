
# Project Root Namespaces

Each project in this solution uses the following RootNamespace (from the .csproj):

- **feedbackwebapp**: `FeedbackWebApp`
- **feedbackfunctions**: `FeedbackFunctions`
- **shareddump**: `SharedDump`
- **FeedbackFlow.AppHost**: `FeedbackFlow.AppHost`
- **FeedbackFlow.ServiceDefaults**: `FeedbackFlow.ServiceDefaults`
- **feedbackflow.tests**: `FeedbackFlow.Tests`

# Copilot Instructions

The github repo is jamesmontemagno/feedbackflow and the primary branch that I work off of is main

## Core Commands

### Building and Testing
- **Build entire solution**: `dotnet build FeedbackFlow.slnx --configuration Release`
- **Run all tests**: `dotnet test FeedbackFlow.slnx --configuration Release`
- **Run specific test project**: `dotnet test feedbackflow.tests/Tests.csproj`
- **Run single test class**: `dotnet test feedbackflow.tests/Tests.csproj --filter ClassName=GitHubUrlParserTests`
- **Restore dependencies**: `dotnet restore FeedbackFlow.slnx`

### Development Workflow
- **Run full application with the aspire CLI** (recommended): `aspire run`
- **Run web app only**: `dotnet run --project feedbackwebapp/WebApp.csproj`
- **Build functions only**: Available as VS Code task "build (functions)"
- **Run functions standalone**: Available as VS Code task with func host
- **Clean build**: Available as VS Code task "clean (functions)"

## Architecture Overview

### Tech Stack
- **.NET 9** with C# latest features, file-scoped namespaces
- **Blazor Server** for interactive web UI with real-time updates
- **Azure Functions** (.NET 9) for serverless backend APIs
- **.NET Aspire** for local orchestration and service discovery
- **Bootstrap** + custom CSS variables for responsive theming

### Core Projects
- **feedbackwebapp** (WebApp.csproj): Main Blazor Server application
- **feedbackfunctions** (Functions.csproj): Azure Functions backend APIs
- **shareddump** (Shared.csproj): Shared models, services, utilities
- **FeedbackFlow.AppHost** (AppHost.csproj): .NET Aspire orchestration
- **feedbackflow.tests** (Tests.csproj): MSTest unit/integration tests

### External Integrations
- **Azure OpenAI**: Sentiment analysis and AI insights
- **GitHub API**: Issues, PRs, discussions data
- **YouTube Data API**: Video comments collection
- **Reddit API**: Posts and comments
- **Hacker News API**: Stories and discussions
- **Azure Storage**: File/blob storage for reports and caching

## Project Structure
- **feedbackflow.tests** - MSTest unit tests for shared logic and service integrations
- **feedbackfunctions** - Azure Functions backend with HTTP triggers, timers, blob bindings
- **feedbackwebapp** - Blazor Server app with components, services, and responsive UI
- **shareddump** - Shared library containing reusable logic and models
- **FeedbackFlow.ServiceDefaults** - Aspire service defaults and telemetry

## Blazor
- Always add component-specific CSS in a corresponding .razor.css file
- When creating a new component, automatically create a matching .razor.css file
- Ignore warnings in Blazor components (they are often false positives)
- Use scoped CSS through the .razor.css pattern instead of global styles
- Make sure light and dark theme are respected throughout by never using hard coded rgb or hex but that they are always defined in the main css

## CSS Best Practices

### General Styling
- Use Bootstrap's built-in spacing utilities (m-*, p-*) for consistent spacing
- Always wrap card content in a .card-body element for consistent padding
- Use semantic class names that describe the component's purpose (e.g., `report-metadata`, `history-item`)
- Keep component-specific styles in .razor.css files with proper namespacing
- Define common padding/margin values as CSS variables in app.css
- Avoid direct element styling, prefer class-based selectors
- Use animation keyframes for transitions (fadeIn, slideIn, pulse)
- Style from the component's root element down to maintain CSS specificity

### Theme Support
- Use CSS variables defined in app.css for colors, never hard-coded RGB or HEX values
- Support both light and dark themes via the [data-theme="dark"] selector
- Test all components in both light and dark themes before committing
- Use rgba(var(--primary-color-rgb), opacity) for transparent colors
- Apply color transitions with `transition: color 0.3s ease, background-color 0.3s ease`
- **Bootstrap Override Classes**: When using Bootstrap alert/button classes, override with CSS variables for dark theme support (e.g., .alert-warning, .btn-secondary)
- **Modal Close Buttons**: Ensure .btn-close works in dark theme by applying appropriate filter properties

### Icons & UI Elements
- Use Bootstrap Icons (bi bi-*) with consistent sizing and spacing
- Add icon animations on hover using transform properties
- Format icon containers with proper alignment: `d-flex align-items-center gap-2`
- Include appropriate ARIA attributes on interactive icons

### Card & Component Design
- Apply consistent border-radius using var(--border-radius) variables
- Use var(--card-shadow) for box-shadow on cards and raised elements
- Apply subtle hover effects: transform, box-shadow, or background-color changes
- Create clean card headers with the primary gradient background
- Use primary-gradient for accent elements: `background: var(--primary-gradient)`

### Responsive Design
- Use responsive grids with Bootstrap's column system or CSS Grid
- Implement dedicated media queries for mobile adjustments at standard breakpoints
- Test on multiple viewport sizes: desktop, tablet, and mobile
- Avoid fixed pixel values for responsive elements, prefer rem/em units
- Adapt to smaller screens by adjusting spacing, font size, and layout

### Best Practices
- Use rem/em units for font sizes and spacing for better accessibility
- Document any magic numbers or non-obvious style choices in comments
- Maintain semantic HTML structure with proper heading hierarchy
- Include responsive adjustments at the bottom of CSS files
- Group related CSS rules together with clear comments

### Table Styling Patterns
For consistent table design across admin interfaces, follow these established patterns:

**HTML Structure:**
```html
<div class="table-responsive">
    <table class="table table-hover align-middle admin-[component]-table">
        <thead>
            <tr>
                <th scope="col">Column Name</th>
                <th scope="col" class="d-none d-md-table-cell">Hidden on Mobile</th>
                <th scope="col" class="text-center">Actions</th>
            </tr>
        </thead>
        <tbody>
            <tr>
                <td>
                    <div class="item-name">Primary Content</div>
                </td>
                <td class="d-none d-md-table-cell">
                    <span class="item-date">
                        <i class="bi bi-calendar me-1"></i>
                        Secondary Content
                    </span>
                </td>
                <td class="text-center">
                    <div class="action-buttons">
                        <button class="btn btn-sm btn-outline-primary" title="Action">
                            <i class="bi bi-pencil"></i>
                        </button>
                    </div>
                </td>
            </tr>
        </tbody>
    </table>
</div>
```

**CSS Class Structure:**
- `.admin-[component]-table` - Main table class with custom styling
- `.table-responsive` - Bootstrap wrapper for horizontal scrolling
- `.d-none .d-md-table-cell` - Responsive column hiding
- `.action-buttons` - Container for action button groups
- `.status-badge` - Styled status indicators with theme support
- `.item-date` - Date/time display with icons
- `.item-name` - Primary content styling

**Key CSS Properties:**
```css
.admin-[component]-table {
    width: 100%;
    margin-bottom: 0;
    background: var(--card-bg);
}

.admin-[component]-table th {
    background: rgba(var(--bg-secondary-rgb), 0.8);
    color: var(--text-primary);
    font-weight: 600;
    text-transform: uppercase;
    font-size: 0.875rem;
    letter-spacing: 0.5px;
    border-bottom: 2px solid var(--border-color);
}

.admin-[component]-table tbody tr:hover td {
    background: linear-gradient(135deg, rgba(var(--primary-color-rgb), 0.05) 0%, rgba(var(--primary-color-rgb), 0.1) 100%);
}
```

**Status Badge Pattern:**
```css
.status-badge {
    display: inline-flex;
    align-items: center;
    padding: 0.25rem 0.75rem;
    border-radius: var(--border-radius-pill);
    font-size: 0.75rem;
    font-weight: 600;
    text-transform: uppercase;
    letter-spacing: 0.5px;
    gap: 0.25rem;
    transition: all 0.3s ease;
}
```

**Dark Theme Requirements:**
- Use `[data-theme="dark"]` selector for dark mode overrides
- Apply enhanced contrast for table hover states
- Ensure status badges maintain readability in both themes
- Use CSS variables exclusively, never hard-coded colors

**Responsive Breakpoints:**
- `@media (max-width: 991.98px)` - Tablet adjustments
- `@media (max-width: 767.98px)` - Mobile layout changes
- `@media (max-width: 575.98px)` - Small mobile optimizations

See AdminReports.razor and AdminApiKeyManagement.razor for reference implementations.

## Code Style
- Prefer async/await over direct Task handling
- Use nullable reference types
- Use var over explicit type declarations 
- Always implement IDisposable when dealing with event handlers or subscriptions
- Prefer using async/await for asynchronous operations
- Use latest C# features (e.g., records, pattern matching)
- Use consistent naming conventions (PascalCase for public members, camelCase for private members)
- Use meaningful names for variables, methods, and classes
- Use dependency injection for services and components
- Use interfaces for service contracts and put them in a unique file
- Use file scoped namespaces in C# and are PascalCased
- Always add namespace declarations to Blazor components matching their folder structure
- Organize using directives:
  - Put System namespaces first
  - Put Microsoft namespaces second
  - Put application namespaces last
  - Remove unused using directives
  - Sort using directives alphabetically within each group

## Component Structure
- Keep components small and focused
- Extract reusable logic into services
- Use cascading parameters sparingly
- Prefer component parameters over cascading values

## Error Handling
- Use try-catch blocks in event handlers
- Implement proper error boundaries
- Display user-friendly error messages
- Log errors appropriately
- **Usage Limit Errors**: Check for JSON error responses with "USAGE_LIMIT_EXCEEDED" ErrorCode and display UsageLimitDialog instead of raw error messages

## Performance
- Implement proper component lifecycle methods
- Use @key directive when rendering lists
- Avoid unnecessary renders
- Use virtualization for large lists

## Testing
- Write unit tests for complex component logic only if i ask for tests
- Test error scenarios
- Mock external dependencies
- Use MSTest for component testing
- Create tests in the feedbackflow.tests project

## Documentation
- Document public APIs
- Include usage examples in comments
- Document any non-obvious behavior
- Keep documentation up to date

## Security
- Always validate user input

## Accessibility
- Use semantic HTML
- Include ARIA attributes where necessary
- Ensure keyboard navigation works

## File Organization
- Keep related files together
- Use meaningful file names
- Follow consistent folder structure
- Group components by feature when possible

### Azure Functions Development
- **Local settings**: Create `feedbackfunctions/local.settings.json` with required API keys
- **Storage emulator**: Uses `AzureWebJobsStorage: "UseDevelopmentStorage=true"` for local development  
- **Required API keys**: GitHub PAT, YouTube API key, Azure OpenAI endpoint/key, Reddit credentials
- **Functions runtime**: `dotnet-isolated` (.NET 9)
- **Key endpoints**: SaveSharedAnalysis, GetSharedAnalysis, GitHubIssuesReport, WeeklyReportProcessor

### Package Management
- Uses **Central Package Management** via `Directory.Packages.props`
- Key packages: Azure.AI.OpenAI, Azure.Data.Tables, Microsoft.Azure.Functions.Worker, Blazor.SpeechSynthesis
- All projects target **.NET 9** with nullable reference types enabled

## Domain Architecture & Data Flow

### Core Domain Models
- **CommentData**: Universal comment structure supporting nested replies across all platforms (GitHub, YouTube, Reddit, etc.)
- **CommentThread**: Container for posts/videos with associated comments, includes platform-specific metadata via JsonExtensionData
- **AnalysisData**: AI-generated insights with title, summary, full analysis, and source tracking
- **Platform-Specific Models**: Each platform (GitHub, YouTube, Reddit, etc.) has dedicated models in `shareddump/Models/[Platform]/`

### Service Layer Patterns
- **Mock Services**: All external APIs have mock implementations for local development (UseMocks=true in debug mode)
- **Service Adapters**: Convert platform-specific models to universal CommentData/CommentThread structure
- **Streaming Analysis**: AI services use `IAsyncEnumerable<string>` for real-time analysis streaming to UI

### Data Collection Flow
1. **Web Application** collects feedback data through direct API integrations
2. **Service Adapters** normalize data to universal models
3. **Azure Functions** expose HTTP endpoints for web app consumption
4. **Cache Layer** (`ReportCacheService`) stores processed reports in Azure Blob Storage

### Authentication & Security Patterns
- **Function-level auth**: All Azure Functions use `AuthorizationLevel.Function`
- **Web app password**: Single shared password for demo access via `FeedbackApp__AccessPassword`
- **API Key Management**: Platform APIs use conditional mock fallback when keys unavailable
- **User Secrets**: Local development uses user secrets, production uses environment variables

## Development Patterns

### Error Handling Convention
```csharp
try
{
    // Operation logic
    var response = req.CreateResponse(HttpStatusCode.OK);
    await response.WriteAsJsonAsync(result);
    return response;
}
catch (Exception ex)
{
    _logger.LogError(ex, "Error message with context");
    var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
    await errorResponse.WriteStringAsync("User-friendly error message");
    return errorResponse;
}
```

### Usage Limit Error Pattern
Use the shared `UsageLimitErrorHelper.TryParseUsageLimitError()` method for consistent error handling:
```csharp
catch (Exception ex)
{
    // Check if this is a usage limit error
    if (UsageLimitErrorHelper.TryParseUsageLimitError(ex.Message, out var limitError))
    {
        usageLimitError = limitError;
        showUsageLimitDialog = true;
    }
    else
    {
        // Handle regular errors
        error = $"An error occurred: {ex.Message}";
    }
}
```

Add required using directive: `@using SharedDump.Utils`

### Service Registration Pattern
Services are registered conditionally based on `UseMocks` configuration:
```csharp
if (useMocks)
{
    builder.Services.AddScoped<IGitHubService, MockGitHubService>();
}
else
{
    builder.Services.AddScoped<IGitHubService>(serviceProvider => {
        var token = configuration["GitHub:AccessToken"];
        return string.IsNullOrWhiteSpace(token) 
            ? new MockGitHubService() 
            : new GitHubService(token, httpClient);
    });
}
```

### New Service Creation Pattern
When creating new services that communicate with Azure Functions, follow this pattern:

**1. Service Interface & Implementation:**
```csharp
public class MyService : IMyService
{
    private readonly HttpClient _httpClient;
    private readonly IAuthenticationHeaderService _headerService;
    private readonly ILogger<MyService> _logger;
    private readonly string _baseUrl;
    private readonly string _functionsKey;

    public MyService(
        HttpClient httpClient,
        IAuthenticationHeaderService headerService,
        ILogger<MyService> logger,
        IConfiguration configuration)
    {
        _httpClient = httpClient;
        _headerService = headerService;
        _logger = logger;
        _baseUrl = configuration["FeedbackApi:BaseUrl"]
            ?? throw new InvalidOperationException("FeedbackApi:BaseUrl not configured");
        _functionsKey = configuration["FeedbackApi:FunctionsKey"]
            ?? throw new InvalidOperationException("FeedbackApi:FunctionsKey not configured");
    }
}
```

**2. API Calls with Authentication:**
```csharp
var request = new HttpRequestMessage(HttpMethod.Get, 
    $"{_baseUrl}/api/MyEndpoint?code={Uri.EscapeDataString(_functionsKey)}");
await _headerService.AddAuthenticationHeadersAsync(request);
var response = await _httpClient.SendAsync(request);
```

**3. Mock Service Implementation:**
```csharp
public class MockMyService : IMyService
{
    private readonly ILogger<MockMyService> _logger;
    
    public MockMyService(ILogger<MockMyService> logger)
    {
        _logger = logger;
    }
    
    public async Task<SomeModel> GetDataAsync()
    {
        await Task.Delay(200); // Simulate network delay
        _logger.LogInformation("Mock: Returning sample data");
        return new SomeModel(); // Return mock data
    }
}
```

**4. Service Registration in Program.cs:**
```csharp
builder.Services.AddScoped<IMyService>(serviceProvider =>
{
    if (useMocks)
    {
        var logger = serviceProvider.GetRequiredService<ILogger<MockMyService>>();
        return new MockMyService(logger);
    }
    else
    {
        var httpClient = serviceProvider.GetRequiredService<IHttpClientFactory>().CreateClient("DefaultClient");
        var headerService = serviceProvider.GetRequiredService<IAuthenticationHeaderService>();
        var logger = serviceProvider.GetRequiredService<ILogger<MyService>>();
        var configuration = serviceProvider.GetRequiredService<IConfiguration>();
        return new MyService(httpClient, headerService, logger, configuration);
    }
});
```

**Key Requirements:**
- Always use `Configuration["FeedbackApi:BaseUrl"]` for base URL
- Always use `Configuration["FeedbackApi:FunctionsKey"]` and add as `?code=` query parameter
- Always add authentication headers via `IAuthenticationHeaderService`
- Always use the "DefaultClient" HttpClient from factory
- Always create corresponding mock service for development
- Always register both real and mock services conditionally based on `useMocks`

### Blazor Component Structure
- **Page Components**: Located in `Components/Pages/`, include `@namespace` declarations
- **Shared Components**: In `Components/Shared/`, reusable across pages
- **Form Components**: In `Components/Feedback/Forms/`, handle user input and validation
- **Result Components**: In `Components/Feedback/Results/`, display analysis results
- **Error Dialogs**: Use `UsageLimitDialog` for usage limit exceeded errors, ensure dark theme compatibility

### JSON Serialization Context
Uses source-generated JSON serialization contexts for better performance:
- `FeedbackJsonContext` in Functions project for API models
- Platform-specific contexts like `TwitterFeedbackJsonContext`, `BlueSkyFeedbackJsonContext`

### Testing Strategy
- **MSTest** with **NSubstitute** for mocking
- Focus on business logic, URL parsing, data transformations
- **ReportCacheServiceTests**, **UrlParsingTests**, **GitHubIssuesUtilsTests** show patterns
- Test naming: `MethodName_Scenario_ExpectedResult`

## Critical Integration Points

### .NET Aspire Orchestration
- **AppHost**: Orchestrates web app + functions with service discovery
- **Storage Emulator**: Uses Azurite for local blob storage during development
- **Environment Variables**: Functions get configuration via Aspire's environment context
- **API Key Prompting**: Interactive setup via `KeyConfiguration.PromptForValues()`

### Azure Functions Binding Patterns
- **HTTP Triggers**: All use `HttpRequestData` and create `HttpResponseData`
- **Blob Bindings**: Report caching uses blob containers: `shared-analyses`, `reports`, `hackernews-cache`
- **Timer Functions**: Background processing for scheduled reports (in Reports/ folder)

### Cross-Component Communication
- **Web App → Functions**: Uses `FeedbackServiceProvider` with configured base URL
- **Shared Models**: `shareddump` project provides DTOs shared between all components
- **Service Discovery**: Aspire handles service-to-service communication via named endpoints

### Platform API Patterns
- **GitHub**: Uses GraphQL for issues/discussions, REST for comments
- **YouTube**: Paginated video/comment collection with quota management
- **Reddit**: OAuth2 client credentials flow, handles shortlink resolution
- **Hacker News**: Public API, recursive comment tree traversal