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
./ghdump --r <repository>
```

#### YouTube Comment Collector (`ytdump`)

To gather comments from YouTube:

```bash
./ytdump --v <video-id> --output <output-file>
```

## License

This project is licensed under the MIT License. See the [LICENSE](./LICENSE) file for details.

