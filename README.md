# FeedbackFlow

## Overview

FeedbackFlow is a set of tools designed to collect and consolidate feedback from GitHub issues, discussions, and YouTube comments into a machine-readable format (JSON). With dedicated components for different platforms, FeedbackFlow simplifies the process of centralizing feedback for analysis and decision-making.

## Components

### GitHub Feedback Collector (`ghdump`)

This tool retrieves data from GitHub issues and discussions, including associated comments. The collected information is saved as JSON files, enabling easy integration with other tools.

### YouTube Comment Collector (`ytdump`)

This tool gathers comments from specified YouTube videos or playlists and exports them into JSON format. Input sources can be configured through a file or directly via command-line arguments.

## Getting Started

### Prerequisites

To use FeedbackFlow, ensure you have the following:

- .NET 9.0 SDK installed
- A valid GitHub API Key
- A valid YouTube API Key

### Installation

1. Clone the repository:
   ```bash
   git clone https://github.com/davidfowl/feedbackflow.git
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

### Usage

#### GitHub Feedback Collector (`ghdump`)

To collect feedback from GitHub:

```bash
./ghdump -r <repository>
```

#### YouTube Comment Collector (`ytdump`)

To gather comments from YouTube:

```bash
./ytdump -v <video-id> -o <output-file.json>
```

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

## License

This project is licensed under the MIT License. See the [LICENSE](./LICENSE) file for details.

