
# Project Root Namespaces

Each project in this solution uses the following RootNamespace (from the .csproj):

- **feedbackwebapp**: `FeedbackWebApp`
- **feedbackfunctions**: `FeedbackFunctions`
- **shareddump**: `SharedDump`
- **FeedbackFlow.AppHost**: `FeedbackFlow.AppHost`
- **FeedbackFlow.ServiceDefaults**: `FeedbackFlow.ServiceDefaults`
- **feedbackflow.tests**: `FeedbackFlow.Tests`

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

## PR instructions
- Title format: [<project_name>] <Title>
- Always run `dotnet build` and `dotnet test` before committing.