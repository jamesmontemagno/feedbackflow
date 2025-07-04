# SharedHistoryService Usage Example

## Overview

The `SharedHistoryService` provides functionality to manage user's saved shared analyses. It includes both a real implementation that communicates with Azure Functions and a mock implementation for testing.

## Service Registration

The service is registered using the provider pattern in `Program.cs`:

```csharp
builder.Services.AddScoped<ISharedHistoryServiceProvider, SharedHistoryServiceProvider>();
```

## Usage in Components

### Basic Usage

```csharp
@page "/shared-history"
@using FeedbackWebApp.Services.Interfaces
@inject ISharedHistoryServiceProvider SharedHistoryServiceProvider
@inject ILogger<SharedHistoryPage> Logger

<h3>My Saved Analyses</h3>

@if (savedAnalyses == null)
{
    <p>Loading...</p>
}
else if (!savedAnalyses.Any())
{
    <p>No saved analyses found.</p>
}
else
{
    <div class="analyses-grid">
        @foreach (var analysis in savedAnalyses)
        {
            <div class="analysis-card">
                <h4>@analysis.Title</h4>
                <p class="summary">@analysis.Summary</p>
                <div class="metadata">
                    <span class="source">@analysis.SourceType</span>
                    <span class="date">@analysis.CreatedDate.ToString("yyyy-MM-dd")</span>
                </div>
                <div class="actions">
                    <button @onclick="() => ViewAnalysis(analysis.Id)">View</button>
                    <button @onclick="() => DeleteAnalysis(analysis.Id)" class="btn-danger">Delete</button>
                </div>
            </div>
        }
    </div>
}

@code {
    private List<SharedAnalysisEntity>? savedAnalyses;
    private ISharedHistoryService? sharedHistoryService;

    protected override async Task OnInitializedAsync()
    {
        sharedHistoryService = SharedHistoryServiceProvider.GetService();
        await LoadSavedAnalyses();
    }

    private async Task LoadSavedAnalyses()
    {
        try
        {
            savedAnalyses = await sharedHistoryService!.GetUsersSavedAnalysesAsync();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error loading saved analyses");
            savedAnalyses = new List<SharedAnalysisEntity>();
        }
    }

    private async Task DeleteAnalysis(string id)
    {
        try
        {
            var success = await sharedHistoryService!.DeleteSharedAnalysisAsync(id);
            if (success)
            {
                await LoadSavedAnalyses(); // Refresh the list
            }
            else
            {
                Logger.LogWarning("Failed to delete analysis {Id}", id);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error deleting analysis {Id}", id);
        }
    }

    private async Task ViewAnalysis(string id)
    {
        try
        {
            var analysisData = await sharedHistoryService!.GetSharedAnalysisDataAsync(id);
            if (analysisData != null)
            {
                // Navigate to analysis view or open modal
                // NavigationManager.NavigateTo($"/analysis/{id}");
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error loading analysis data for {Id}", id);
        }
    }
}
```

### Search Functionality

```csharp
<div class="search-section">
    <input @bind="searchTerm" @onkeyup="OnSearchKeyUp" placeholder="Search analyses..." />
    <button @onclick="SearchAnalyses">Search</button>
</div>

@code {
    private string searchTerm = string.Empty;

    private async Task OnSearchKeyUp(KeyboardEventArgs e)
    {
        if (e.Key == "Enter")
        {
            await SearchAnalyses();
        }
    }

    private async Task SearchAnalyses()
    {
        try
        {
            savedAnalyses = await sharedHistoryService!.SearchUsersSavedAnalysesAsync(searchTerm);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error searching analyses");
        }
    }
}
```

## Configuration

### Development/Mock Mode

In `appsettings.Development.json`:

```json
{
  "FeedbackApi": {
    "UseMocks": true,
    "BaseUrl": "http://localhost:7071",
    "FunctionsKey": "mock-key"
  }
}
```

### Production Mode

In `appsettings.json`:

```json
{
  "FeedbackApi": {
    "UseMocks": false,
    "BaseUrl": "https://your-functions-app.azurewebsites.net",
    "FunctionsKey": "your-actual-function-key"
  }
}
```

## API Methods

### GetUsersSavedAnalysesAsync()
Returns all saved analyses for the authenticated user, sorted by creation date (newest first).

### DeleteSharedAnalysisAsync(string id)
Deletes a specific analysis if the user owns it. Returns `true` if successful.

### GetSharedAnalysisDataAsync(string id)
Gets the complete analysis data including the full analysis content.

### RefreshUsersSavedAnalysesAsync()
Forces a refresh of the cached analyses from the server.

### GetSavedAnalysesCountAsync()
Returns the count of saved analyses for quick statistics.

### SearchUsersSavedAnalysesAsync(string searchTerm)
Searches through saved analyses by title, summary, source type, and user input.

## Error Handling

The service includes comprehensive error handling and logging. All methods are designed to gracefully handle failures and return empty results rather than throwing exceptions.

## Caching

The real implementation includes in-memory caching with a 10-minute expiration to improve performance and reduce API calls.

## Mock Data

The mock service provides realistic sample data including:
- GitHub repository analyses
- YouTube video comment analyses  
- Reddit discussion analyses
- Hacker News feedback analyses
- Manual survey analyses

This enables development and testing without requiring backend connectivity.
