# Production Storage Configuration

This document explains the new `ProductionStorage` configuration option that separates application data storage from Azure Functions runtime storage.

## Overview

Prior to this change, all Azure Functions used the `AzureWebJobsStorage` connection string for both:
- Azure Functions runtime operations (logs, triggers, state)
- Application data storage (reports, shared analyses, user authentication tables, caches)

With the new `ProductionStorage` configuration, you can now use separate storage accounts for better isolation, security, and cost management.

## Configuration

### Local Development

In `local.settings.json`:
```json
{
  "Values": {
    "AzureWebJobsStorage": "UseDevelopmentStorage=true",
    "ProductionStorage": "UseDevelopmentStorage=true"
  }
}
```

Both settings point to the local Azurite emulator for development.

### Production Deployment

In your Azure Functions App Settings:
```
AzureWebJobsStorage = DefaultEndpointsProtocol=https;AccountName=functionsruntime;AccountKey=...
ProductionStorage = DefaultEndpointsProtocol=https;AccountName=appdata;AccountKey=...
```

- `AzureWebJobsStorage`: Functions runtime storage (required by Azure Functions)
- `ProductionStorage`: Application data storage (your choice of storage account)

## What Uses ProductionStorage

The following components now use the `ProductionStorage` connection string:

### Azure Functions
- **Blob Bindings**: All `[BlobInput]` and `[BlobOutput]` attributes use `Connection = "ProductionStorage"`
- **Table Storage**: Report requests, user authentication tables
- **Blob Storage**: Reports cache, shared analyses, HackerNews cache

### Specific Functions Updated
- `SharingFunctions`: SaveSharedAnalysis, GetSharedAnalysis, CleanupOldAnalyses
- `ContentFeedFunctions`: SearchHackerNewsArticles, CacheHackerNewsArticlesHourly
- `ReportRequestFunctions`: All table and blob operations
- `ReportProcessorFunctions`: All table and blob operations
- `ReportingFunctions`: All blob operations
- `AuthUserTableService`: User authentication tables

### Storage Containers/Tables Used
- **Blob Containers**: `shared-analyses`, `reports`, `hackernews-cache`, `weekly-summaries`
- **Tables**: `reportrequests`, `AuthUsers`, `UserEmailIndex`

## Connection String Priority

The authentication service (`AuthUserTableService`) uses this priority order:
1. `ProductionStorage` (new setting)
2. `AzureWebJobsStorage` (fallback)

## Benefits

### Security
- Isolate application data from Functions runtime data
- Use different access keys for different purposes
- Apply different security policies to each storage account

### Cost Management
- Monitor costs separately for runtime vs. application data
- Use different storage tiers (Hot/Cool/Archive) as appropriate
- Apply different retention policies

### Scalability
- Reduce contention on the Functions runtime storage
- Scale application data storage independently
- Use geo-replication for application data if needed

## Migration

This change is backward compatible:
- If `ProductionStorage` is not configured, the system will throw a clear error message
- Local development continues to work with Azurite
- No data migration is required - just configure the new setting

## Environment Variables for Production

When deploying to Azure, set these environment variables:

```bash
# Azure Functions App Settings
az functionapp config appsettings set --name your-function-app --resource-group your-rg --settings \
  "ProductionStorage=DefaultEndpointsProtocol=https;AccountName=your-app-data-storage;AccountKey=your-key"
```

## Troubleshooting

### Error: "Production storage connection string not configured"
- Ensure `ProductionStorage` is set in your environment
- For local development, check `local.settings.json`
- For production, check Azure Functions App Settings

### Blob/Table operations failing
- Verify the storage account exists and is accessible
- Check that the connection string is valid
- Ensure the storage account has the required containers/tables (they're created automatically on first use)
