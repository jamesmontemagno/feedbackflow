
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

### Blazor Component Structure
- **Page Components**: Located in `Components/Pages/`, include `@namespace` declarations
- **Shared Components**: In `Components/Shared/`, reusable across pages
- **Form Components**: In `Components/Feedback/Forms/`, handle user input and validation
- **Result Components**: In `Components/Feedback/Results/`, display analysis results

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