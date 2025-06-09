# FeedbackFlow

## Overview

FeedbackFlow is a comprehensive feedback collection and analysis platform that helps teams gather, consolidate, and analyze feedback from multiple sources including GitHub, YouTube, Reddit, Hacker News, and social media platforms. The platform consists of a modern Blazor web application, Azure Functions backend, and command-line tools for data collection.

## Live Application

🌐 **Production:** [https://feedbackflow.app](https://feedbackflow.app)  
🧪 **Staging:** [https://staging.feedbackflow.app](https://staging.feedbackflow.app)

## Architecture

FeedbackFlow follows a modern .NET architecture with the following components:

### 🌐 Web Application (`feedbackwebapp`)
- **Blazor Server** application built with .NET 9
- Modern, responsive UI with light/dark theme support
- Real-time feedback collection and analysis
- Interactive dashboards and reporting
- User authentication and session management

### ⚡ Azure Functions (`feedbackfunctions`)
- Serverless backend for data processing
- RESTful APIs for the web application
- Background processing for large data collections
- Integration with AI services for sentiment analysis
- Scheduled tasks for automated data updates

### 📚 Shared Library (`shareddump`)
- Common models and utilities
- Data transfer objects (DTOs)
- Business logic shared between components
- Serialization and validation helpers

### 🛠️ Command-Line Tools
Collection tools for different platforms:

#### GitHub Feedback Collector (`ghdump`)
Retrieves data from GitHub issues, discussions, and pull requests with associated comments.

#### YouTube Comment Collector (`ytdump`)
Gathers comments from YouTube videos and playlists, supporting bulk operations.

#### Reddit Collector (`rddump`)
Extracts posts and comments from Reddit discussions and threads.

#### Hacker News Collector (`hndump`)
Collects stories and comments from Hacker News discussions.

### 🧪 Testing (`feedbackflow.tests`)
Comprehensive test suite using MSTest for:
- URL parsing and validation
- Data transformation logic
- Service integrations
- Business rule validation

### 🔧 Model Context Protocol Server (`feedbackmcp`)
*Work in progress* - MCP server for AI integration and automated analysis.

## Features

### 📊 Multi-Platform Data Collection
- **GitHub**: Issues, discussions, pull requests, and comments
- **YouTube**: Video comments and playlist discussions
- **Reddit**: Posts, comments, and thread discussions
- **Hacker News**: Stories and comment threads
- **Social Media**: Twitter/X and BlueSky integration

### 🤖 AI-Powered Analysis
- Sentiment analysis using Azure OpenAI
- Automated categorization and tagging
- Trend identification and insights
- Content summarization

### 🎨 Modern UI/UX
- Responsive design for all devices
- Light and dark theme support
- Accessible interface with keyboard navigation

## Getting Started

### Prerequisites

To use FeedbackFlow, ensure you have the following:

- .NET 9.0 SDK installed
- A valid GitHub API Key
- A valid YouTube API Key

### Installation

1. Clone the repository:
   ```bash
   git clone https://github.com/jamesmontemagno/feedbackflow.git
   cd feedbackflow
   ```

2. Restore dependencies:
   ```bash
   dotnet restore
   ```

3. Build the project:
   ```bash
   dotnet build
   ```

### Running the Web Application

1. Navigate to the web app directory:
   ```bash
   cd feedbackwebapp
   ```

2. Run the application:
   ```bash
   dotnet run
   ```

3. Open your browser to `https://localhost:7154` (or the URL shown in the terminal)

### Running with .NET Aspire (Recommended for Development)

For the full development experience with orchestration:

1. Navigate to the AppHost directory:
   ```bash
   cd FeedbackFlow.AppHost
   ```

2. Run the orchestrated application:
   ```bash
   dotnet run
   ```

This will start both the web application and Azure Functions locally with proper service discovery.

### Command-Line Tools Usage

#### GitHub Feedback Collector (`ghdump`)

```bash
cd ghdump
dotnet run -- -r <owner/repository>
```

Example:
```bash
dotnet run -- -r microsoft/dotnet
```

#### YouTube Comment Collector (`ytdump`)

```bash
cd ytdump
dotnet run -- -v <video-id> -o <output-file.json>
```

Example:
```bash
dotnet run -- -v dQw4w9WgXcQ -o youtube-comments.json
```

#### Reddit Collector (`rddump`)

```bash
cd rddump
dotnet run -- -u <reddit-url>
```

#### Hacker News Collector (`hndump`)

```bash
cd hndump
dotnet run -- -i <story-id>
```

## Development

### Project Structure

```
FeedbackFlow/
├── feedbackwebapp/          # Blazor Server web application
├── feedbackfunctions/       # Azure Functions backend
├── shareddump/             # Shared library and models
├── FeedbackFlow.AppHost/   # .NET Aspire orchestration
├── feedbackflow.tests/     # Unit and integration tests
├── ghdump/                 # GitHub collection tool
├── ytdump/                 # YouTube collection tool
├── rddump/                 # Reddit collection tool
├── hndump/                 # Hacker News collection tool
└── feedbackmcp/           # MCP server (WIP)
```

### Contributing

1. Fork the repository
2. Create a feature branch: `git checkout -b feature/amazing-feature`
3. Make your changes following the coding guidelines in the instructions
4. Write tests for new functionality
5. Run tests: `dotnet test`
6. Commit your changes: `git commit -m 'Add amazing feature'`
7. Push to the branch: `git push origin feature/amazing-feature`
8. Open a Pull Request

### Coding Standards

- Follow C# naming conventions (PascalCase for public, camelCase for private)
- Use file-scoped namespaces
- Implement proper async/await patterns
- Add component-specific CSS in `.razor.css` files for Blazor components
- Support both light and dark themes
- Write meaningful commit messages

## Deployment

### Azure Deployment

The application is configured for deployment to Azure with:

- **Azure App Service** for the web application
- **Azure Functions** for the serverless backend
- **GitHub Actions** for CI/CD with staging and production environments

### Environment Configuration

Set up the following environments:
- **Production**: `https://feedbackflow.app`
- **Staging**: `https://staging.feedbackflow.app` (deployed on PR creation)

## Azure Functions Configuration

### Local Development Setup

To run the Azure Functions project locally, you'll need to configure the `local.settings.json` file in the `feedbackfunctions` directory. Create the file with the following structure:

```json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "UseDevelopmentStorage=true",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
    "GitHub:AccessToken": "your_github_pat_here",
    "YouTube:ApiKey": "your_youtube_api_key_here",
    "Azure:OpenAI:Endpoint": "your_azure_openai_endpoint",
    "Azure:OpenAI:ApiKey": "your_azure_openai_key",
    "Azure:OpenAI:Deployment": "your_model_deployment_name"
  }
}
```

### Required API Keys and Configuration

1. **GitHub Personal Access Token (PAT)**
   - Create a GitHub PAT with `repo` scope
   - Set it in `GitHub:AccessToken`

2. **YouTube API Key**
   - Create a project in Google Cloud Console
   - Enable YouTube Data API v3
   - Create API credentials
   - Set the key in `YouTube:ApiKey`

3. **Azure OpenAI Configuration**
   - Create an Azure OpenAI resource
   - Set the endpoint URL in `Azure:OpenAI:Endpoint`
   - Set the API key in `Azure:OpenAI:ApiKey`
   - Deploy a model and set its name in `Azure:OpenAI:Deployment`

4. **Azure Storage Emulator**
   - Install Azure Storage Emulator for local development
   - The default connection string is already set in `AzureWebJobsStorage`

### Running the Functions

After configuring the settings:

```bash
cd feedbackfunctions
func start
```

> Note: Keep your API keys and tokens secure and never commit them to source control.

## Azure Functions local.settings.json

You will need the following:
```json
{
   "IsEncrypted": false,
   "Values": {
      "AzureWebJobsStorage": "UseDevelopmentStorage=true",
      "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated"
   },
   "YouTube:ApiKey": "YOUR_API_KEY_HERE",
   "Reddit:ClientId": "YOUR_REDDIT_CLIENT_ID",
   "Reddit:ClientSecret": "YOUR_REDDIT_CLIENT_SECRET", 
   "GitHub:AccessToken": "YOUR_ACCESS_TOKEN_HERE",
   "Azure:OpenAI:Endpoint": "YOUR_AZURE_OPENAI_ENDPOINT",
   "Azure:OpenAI:ApiKey": "YOUR_AZURE_OPENAI_API_KEY",
   "Azure:OpenAI:Deployment": "YOUR_DEPLOYMENT_NAME",
   "Twitter:BearerToken": "YOUR_TWITTER_BEARER_TOKEN",
   "BlueSky:Username": "YOUR_BLUESKY_USERNAME",
   "BlueSky:AppPassword": "YOUR_BLUESKY_APP_PASSWORD"
}
```

## License

This project is licensed under the MIT License. See the [LICENSE](./LICENSE) file for details.

