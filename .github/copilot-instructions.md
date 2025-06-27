# Copilot Instructions

The github repo is jamesmontemagno/feedbackflow and the primary branch that I work off of is main

## Core Commands

### Build & Test
- **Build solution**: `dotnet build FeedbackFlow.slnx --configuration Release`
- **Restore packages**: `dotnet restore FeedbackFlow.slnx`
- **Run tests**: `dotnet test FeedbackFlow.slnx --configuration Release`
- **Single test file**: `dotnet test feedbackflow.tests/[TestFileName].cs`

### Development
- **Run web app**: `cd feedbackwebapp && dotnet run`
- **Run with Aspire**: `cd FeedbackFlow.AppHost && dotnet run`
- **Run Azure Functions**: `cd feedbackfunctions && func start` (requires Azure Functions Core Tools)
- **CLI tools**: `cd [ghdump|ytdump|rddump|hndump] && dotnet run -- [args]`

### Available VS Code Tasks
- `clean (functions)`, `build (functions)`, `publish (functions)` - Azure Functions build pipeline
- `func: 4` - Background task to start Azure Functions with auto-rebuild

## Architecture

### High-Level Components
- **feedbackwebapp** - Blazor Server app (.NET 9) with real-time UI, authentication, theming
- **feedbackfunctions** - Azure Functions (.NET 9) providing serverless backend APIs
- **shareddump** - Shared library with models, services, and business logic
- **CLI tools** - Data collection utilities for GitHub, YouTube, Reddit, Hacker News
- **FeedbackFlow.AppHost** - .NET Aspire orchestration for local development

### Major External Dependencies
- **Azure Services**: Blob Storage, Tables, OpenAI, Functions, App Service
- **APIs**: GitHub (repos, issues, PRs), YouTube Data API v3, Reddit API
- **AI**: Azure OpenAI for sentiment analysis and content processing
- **UI**: Bootstrap 5, Bootstrap Icons, custom CSS variables for theming
- **Data**: JSON serialization, CSV export, PDF reports (QuestPDF), Markdown processing

### Data Flow
1. CLI tools collect data from external APIs → JSON files
2. Web app uploads data → Azure Functions process and analyze
3. Functions use Azure OpenAI for sentiment analysis → store in Blob/Tables
4. Web app displays results with caching and sharing capabilities

## Project Structure
- **feedbackflow.tests** - MSTest unit tests for shared logic and service integrations
- **feedbackfunctions** - Azure Functions backend with HTTP triggers, timers, blob bindings
- **feedbackmcp** - C# MCP server (work in progress, inactive)
- **feedbackwebapp** - Blazor Server app with components, services, and responsive UI
- **shareddump** - Shared library containing reusable logic and models
- **ghdump/ytdump/rddump/hndump** - CLI tools for platform-specific data collection
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