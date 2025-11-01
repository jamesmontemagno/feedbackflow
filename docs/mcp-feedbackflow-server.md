# FeedbackFlow MCP Server

A lightweight Model Context Protocol (MCP) server exposing FeedbackFlow analysis/reporting features to MCP‑compatible clients. Distributed as a ready-to-run Docker image for fast setup (stdio only; no ports).

## What You Get
Tools exposed to an MCP-compatible client:
- `autoanalyze_feedback` – Run AI analysis on a single feedback source URL (GitHub issue, PR/discussion, YouTube video, Reddit post, Hacker News story, etc.)
- `github_issues_report` – Generate a repository issues/discussions activity report
- `reddit_report` – Full subreddit activity & insights report
- `reddit_report_summary` – Condensed subreddit summary

All output is returned as plain text or JSON exactly as provided by the public FeedbackFlow API.

## Quick Start (Docker)
Pull and run the official image (ensure your API key is exported first):
```bash
# 1. Pull image
docker pull jamesmontemagno/feedbackflowmcp:latest

# 2. Run container (foreground)
docker run -i --rm -e FEEDBACKFLOW_API_KEY=ff_your_key_here jamesmontemagno/feedbackflowmcp:latest
```
`-i` keeps STDIN open for MCP JSON‑RPC. No network port is exposed (communication exclusively over stdio).

### Optional Script Wrapper
Linux/macOS (`run-feedbackflow-mcp.sh`):
```bash
#!/usr/bin/env bash
docker run -i --rm \
  -e FEEDBACKFLOW_API_KEY="${FEEDBACKFLOW_API_KEY}" \
  jamesmontemagno/feedbackflowmcp:latest
```
PowerShell (`run-feedbackflow-mcp.ps1`):
```powershell
docker run -i --rm `
  -e FEEDBACKFLOW_API_KEY=$Env:FEEDBACKFLOW_API_KEY `
  jamesmontemagno/feedbackflowmcp:latest
```

## Requirements
| Item | Description |
|------|-------------|
| FeedbackFlow Account | Pro+ tier (or higher) with active API key |
| API Key | Starts with `ff_` (Account Settings) |
| Environment Var | `FEEDBACKFLOW_API_KEY` must be set (or passed) |
| Container Runtime | Docker (or compatible) |
| MCP Client / Extension | VS Code MCP-capable extension or other tooling |
| Outbound HTTPS | Container needs egress to api.feedbackflow.pp |

## Environment Variable
| Name | Required | Purpose |
|------|----------|---------|
| `FEEDBACKFLOW_API_KEY` | Yes | Authenticates all API calls |

Set it (examples):
```bash
export FEEDBACKFLOW_API_KEY=ff_your_key_here       # bash / zsh
$Env:FEEDBACKFLOW_API_KEY = "ff_your_key_here"    # PowerShell
```
Or pass via `-e FEEDBACKFLOW_API_KEY=...` in `docker run`.


## VS Code Integration (remote)

[![Install in VS Code](https://img.shields.io/badge/Install_in-VS_Code-0098FF?style=flat-square&logo=visualstudiocode&logoColor=white)](https://vscode.dev/redirect/mcp/install?name=feedbackflow&config=%7B%22type%22%3A%22http%22%2C%22url%22%3A%22https%3A%2F%2Ffunc-feedbackflow-mcp-rr7fxhjrrtpdq.azurewebsites.net%2Fmcp%22%2C%22headers%22%3A%7B%22Authorization%22%3A%22%24%7Binput%3Afeedbackflow_api_key%7D%22%7D%7D)
[![Install in VS Code Insiders](https://img.shields.io/badge/Install_in-VS_Code_Insiders-24bfa5?style=flat-square&logo=visualstudiocode&logoColor=white)](https://insiders.vscode.dev/redirect/mcp/install?name=feedbackflow&config=%7B%22type%22%3A%22http%22%2C%22url%22%3A%22https%3A%2F%2Ffunc-feedbackflow-mcp-rr7fxhjrrtpdq.azurewebsites.net%2Fmcp%22%2C%22headers%22%3A%7B%22Authorization%22%3A%22%24%7Binput%3Afeedbackflow_api_key%7D%22%7D%7D&quality=insiders)
[![Install in Visual Studio](https://img.shields.io/badge/Install_in-Visual_Studio-C16FDE?style=flat-square&logo=visualstudio&logoColor=white)](https://vs-open.link/mcp-install?%7B%22type%22%3A%22http%22%2C%22url%22%3A%22https%3A%2F%2Ffunc-feedbackflow-mcp-rr7fxhjrrtpdq.azurewebsites.net%2Fmcp%22%2C%22headers%22%3A%7B%22Authorization%22%3A%22%24%7Binput%3Afeedbackflow_api_key%7D%22%7D%7D)


```json
{
  "inputs": [
    {
      "type": "promptString",
      "id": "feedbackflow_api_key",
      "description": "FeedbackFlow API Key (starts with ff_)",
      "password": true
    }
  ],
  "servers": {
    "feedbackflow": {
      "type": "http",
      "url": "https://func-feedbackflow-mcp-rr7fxhjrrtpdq.azurewebsites.net/mcp",
      "headers": {
        "Authorization": "${input:feedbackflow_api_key}"
      }
    }
  }
}
```

## VS Code Integration (local)

[![Install in VS Code](https://img.shields.io/badge/Install_in-VS_Code-0098FF?style=flat-square&logo=visualstudiocode&logoColor=white)](https://vscode.dev/redirect/mcp/install?name=feedbackflow&config=%7B%22command%22%3A%22docker%22%2C%22args%22%3A%5B%22run%22%2C%22-i%22%2C%22--rm%22%2C%22FEEDBACKFLOW_API_KEY%22%5D%2C%22env%22%3A%7B%7D%7D)
[![Install in VS Code Insiders](https://img.shields.io/badge/Install_in-VS_Code_Insiders-24bfa5?style=flat-square&logo=visualstudiocode&logoColor=white)](https://insiders.vscode.dev/redirect/mcp/install?name=feedbackflow&config=%7B%22command%22%3A%22docker%22%2C%22args%22%3A%5B%22run%22%2C%22-i%22%2C%22--rm%22%2C%22FEEDBACKFLOW_API_KEY%22%5D%2C%22env%22%3A%7B%7D%7D&quality=insiders)
[![Install in Visual Studio](https://img.shields.io/badge/Install_in-Visual_Studio-C16FDE?style=flat-square&logo=visualstudio&logoColor=white)](https://vs-open.link/mcp-install?%7B%22command%22%3A%22docker%22%2C%22args%22%3A%5B%22run%22%2C%22-i%22%2C%22--rm%22%2C%22FEEDBACKFLOW_API_KEY%22%5D%2C%22env%22%3A%7B%7D%7D)


If your VS Code setup (via an MCP-enabled extension) supports a user-level JSON configuration similar to the GitHub MCP server example, you can define interactive inputs and map them to environment variables for the container.

### Example MCP Configuration Snippet (Docker)
```jsonc
{
  "inputs": [
    {
      "type": "promptString",
      "id": "feedbackflow_api_key",
      "description": "FeedbackFlow API Key (starts with ff_)",
      "password": true
    }
  ],
  "servers": {
    "feedbackflow": {
      "command": "docker",
      "args": [
        "run",
        "-i",
        "--rm",
        "-e",
        "FEEDBACKFLOW_API_KEY",
        "jamesmontemagno/feedbackflowmcp:latest"
      ],
      "env": {
        "FEEDBACKFLOW_API_KEY": "${input:feedbackflow_api_key}"
      }
    }
  }
}
```
This pattern prompts once for the API key (secured as a password) and injects it into the container environment each run.

### Alternative: Local Build (Instead of Docker)
If you prefer running the local project directly (useful while debugging):
```jsonc
{
  "inputs": [
    {
      "type": "promptString",
      "id": "feedbackflow_api_key",
      "description": "FeedbackFlow API Key (starts with ff_)",
      "password": true
    }
  ],
  "servers": {
    "feedbackflow": {
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "FeedbackFlow.MCP.Remote/FeedbackFlow.MCP.Remote.csproj"
      ],
      "env": {
        "FEEDBACKFLOW_API_KEY": "${input:feedbackflow_api_key}"
      }
    }
  }
}
```

### VS Code Tasks (Optional Convenience)
`.vscode/tasks.json`:
```jsonc
{
  "version": "2.0.0",
  "tasks": [
    {
      "label": "Run FeedbackFlow MCP (Docker)",
      "type": "shell",
      "command": "docker",
      "args": [
        "run",
        "-i",
        "--rm",
        "-e",
        "FEEDBACKFLOW_API_KEY=${FEEDBACKFLOW_API_KEY}",
        "jamesmontemagno/feedbackflowmcp:latest"
      ],
      "problemMatcher": []
    },
    {
      "label": "Run FeedbackFlow MCP (Local Dotnet)",
      "type": "shell",
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "FeedbackFlow.MCP.Remote/FeedbackFlow.MCP.Remote.csproj"
      ],
      "options": {
        "env": {
          "FEEDBACKFLOW_API_KEY": "ff_your_key_here"
        }
      },
      "problemMatcher": []
    }
  ]
}
```
(Replace `ff_your_key_here` or rely on a globally-set environment variable.)

### Debugging Locally
Add `.vscode/launch.json` if you want breakpoints in `FeedbackFlowTools`:
```jsonc
{
  "version": "0.2.0",
  "configurations": [
    {
      "name": "Debug FeedbackFlow MCP",
      "type": "coreclr",
      "request": "launch",
      "program": "dotnet",
      "args": [
        "run",
        "--project",
        "FeedbackFlow.MCP.Remote/FeedbackFlow.MCP.Remote.csproj"
      ],
      "env": {
        "FEEDBACKFLOW_API_KEY": "ff_your_key_here"
      },
      "cwd": "${workspaceFolder}"
    }
  ]
}
```

## Visual Studio Usage
1. Set `FeedbackFlow.MCP.Remote` as Startup Project.
2. Add `FEEDBACKFLOW_API_KEY=ff_your_key_here` under Project Properties > Debug > Environment Variables.
3. F5 to debug (breakpoints in `FeedbackFlowTools` supported) or Ctrl+F5 to run.
4. If integrating with an external MCP-aware tool that launches commands, point it to:
   - `dotnet run --project <full path>/FeedbackFlow.MCP.Remote/FeedbackFlow.MCP.Remote.csproj`
5. For Docker-based launching from Visual Studio, use a custom external tool entry or a simple batch/script file wrapping the `docker run` command.

## Tool Reference
### 1. autoanalyze_feedback
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `url` | string | Yes | Feedback source URL (GitHub issue/PR/discussion, YouTube video, Reddit post, HN story, etc.) |
| `maxComments` | int | No | Limit comments scanned (server may clamp excessively large values) |
| `customPrompt` | string | No | Override default analysis prompt (applies only to type=0 or 2) |
| `type` | int/enum | No | Output mode enum: 0=AnalysisOnly (markdown), 1=CommentsOnly (comments JSON), 2=AnalysisAndComments (combined JSON) |

### 2. github_issues_report
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `repo` | string | Yes | `owner/repo` identifier |
| `force` | bool | No | Regenerate ignoring cache (may consume more quota) |

### 3. reddit_report
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `subreddit` | string | Yes | Subreddit name (omit `r/`) |
| `force` | bool | No | Regenerate ignoring cache |

### 4. reddit_report_summary
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `subreddit` | string | Yes | Subreddit name (omit `r/`) |
| `force` | bool | No | Regenerate ignoring cache |

### Output Format Notes
| Tool | Typical Default Output | Alt Modes |
|------|------------------------|----------|
| autoanalyze_feedback | Markdown analysis (AnalysisOnly / type=0) | JSON (CommentsOnly / type=1, AnalysisAndComments / type=2) |
| github_issues_report | Markdown (narrative) | — |
| reddit_report | Markdown (detailed sections) | — |
| reddit_report_summary | Markdown (condensed) | — |

### Sample JSON (autoanalyze_feedback type=2 / AnalysisAndComments excerpt)
```json
{
  "url": "https://github.com/dotnet/runtime/issues/1",
  "commentCount": 23,
  "comments": [ { "author": "user1", "body": "..." } ],
  "analysis": {
    "summary": "High-level issue themes...",
    "sentiment": "mixed",
    "keyPoints": ["Point A", "Point B"]
  }
}
```

## Manual JSON-RPC (Debug Only)
```json
{"jsonrpc":"2.0","id":1,"method":"tools/list"}
{"jsonrpc":"2.0","id":2,"method":"tools/call","params":{"name":"autoanalyze_feedback","arguments":{"url":"https://github.com/dotnet/runtime/issues/1"}}}
```

## Typical Flow
1. Configure VS Code (or Visual Studio launch) with Docker or dotnet run command.
2. MCP-enabled extension/tool starts the process.
3. Tool invocation sends JSON-RPC over stdio.
4. Output (markdown or JSON) returned to the client.

## Error & Usage Limit Handling
| Case | Example Message / Pattern | Notes |
|------|---------------------------|-------|
| Missing key | `Error: FEEDBACKFLOW_API_KEY environment variable is required` | Set env var before launch |
| HTTP failure | `Error: API call failed with status 401. Response: ...` | Verify key validity / tier |
| Exception | `Error: Exception occurred - <details>` | Check stack / transient network |
| Usage limit | JSON with `"ErrorCode":"USAGE_LIMIT_EXCEEDED"` | Client can detect and surface a quota dialog |

When integrating, parse JSON bodies for `ErrorCode` to gracefully handle quota exhaustion instead of displaying raw text.

## Updating
```bash
docker pull jamesmontemagno/feedbackflowmcp:latest
```
Restart the MCP client/extension to use the updated image.

## Local (Optional) Development Run
```bash
dotnet run --project FeedbackFlow.MCP.Remote/FeedbackFlow.MCP.Remote.csproj
```

## Security Tips
- Never commit your API key.
- Use input-driven secrets (as shown) rather than hardcoding.
- Rotate if exposure suspected.

## FAQ
| Question | Answer |
|----------|--------|
| Do I need another functions key? | No. Only the API key is required. |
| Does it open a port? | No. Communication is stdio only. |
| Can I add more tools? | Yes. Fork, extend `FeedbackFlowTools`, rebuild & republish image. |
| Why is output large sometimes? | Some analyses are verbose; summarize client-side if desired. |

## Image Information
| Detail | Value |
|--------|-------|
| Image | `jamesmontemagno/feedbackflowmcp:latest` |
| Base | .NET 9 runtime (console) |
| Version Tags | `latest` (rolling) – use a digest or future version tag for pinning |
| Protocol | MCP (JSON-RPC over stdio) |

---
For feature requests or issues, open a discussion or issue in the repository.

---
Changelog (doc):
- Added output examples, clarified enum values, expanded error handling, consistency improvements.
