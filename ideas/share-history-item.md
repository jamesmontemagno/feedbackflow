# Idea: Shareable Analysis Feature

## Description
Implement a "Share" functionality that allows users to make their feedback analysis publicly accessible. The system will:
- Add a share button next to the existing save button
- Save the analysis to Azure Blob Storage with a unique GUID
- Store these shared analysis references in the user's local browser history
- Create a dedicated public-facing page that loads shared analysis via a query parameter

This feature enables users to easily share their feedback analysis results with others through a simple URL, without requiring recipients to have accounts or access permissions.

## Acceptance Criteria
- [ ] New "Share" button added next to the existing save button in the analysis UI
- [ ] Azure Function endpoint to upload analysis data to Blob Storage
- [ ] Generation and tracking of unique GUIDs for shared analyses
- [ ] Local browser storage of shared analysis history
- [ ] Public page for viewing shared analysis that loads data based on query parameter
- [ ] Error handling for invalid or expired shared links
- [ ] Loading states during share creation and retrieval
- [ ] User-friendly, accessible UI with proper feedback
- [ ] Component-specific CSS, respecting light/dark themes

## Implementation Plan

1. **Backend Storage Service (Azure Function)**
   - Create a new Azure Function endpoint `SaveSharedAnalysis` that:
     - Accepts analysis data in the request body
     - Generates a unique GUID for the analysis
     - Serializes and stores the data in Azure Blob Storage
     - Returns the GUID to the caller
   - Create a complementary `GetSharedAnalysis` function that:
     - Accepts a GUID as a parameter
     - Retrieves the corresponding analysis from Blob Storage
     - Returns the analysis data or appropriate error

2. **Client-Side Service**
   - Create `IAnalysisSharingService` interface in `shareddump`:
     - Define methods for sharing analysis and retrieving shared analysis
   - Implement `AnalysisSharingService` that:
     - Calls the Azure Functions
     - Handles serialization/deserialization
     - Manages error states

3. **Local Storage Integration**
   - Extend the existing "saved analysis" functionality to track shared analyses
   - Create a model for shared analysis history entries
   - Implement methods to add/retrieve shared analysis history

4. **UI Components**
   - Add Share Button:
     - Create a new button component or extend existing save button
     - Implement click handler to trigger sharing flow
     - Show appropriate loading/success/error states
   - Create a sharing modal/popup:
     - Display the generated sharing URL
     - Include copy-to-clipboard functionality
     - Provide social sharing options (optional)

5. **Shared Analysis Page**
   - Create a new Blazor page `SharedAnalysisViewer.razor`:
     - Read the GUID from the query parameter
     - Call the service to retrieve the shared analysis
     - Display the analysis using existing visualization components
     - Implement loading, error, and empty states
   - Add routing configuration for the new page

6. **Error Handling & UX**
   - Implement error states for:
     - Network errors during sharing/retrieval
     - Missing or invalid GUIDs
     - Expired or deleted shared analyses
   - Provide user feedback for all operations
   - Add confirmation dialogs where appropriate

7. **Styling & Accessibility**
   - Create component-specific CSS files
   - Ensure all new elements are keyboard accessible
   - Use semantic HTML and ARIA attributes
   - Maintain theme consistency with existing app

8. **Testing**
   - Add unit tests for:
     - GUID generation and validation
     - Serialization/deserialization of analysis data
     - Local storage integration
   - Create integration tests for the Azure Functions

9. **Documentation**
   - Document the sharing feature for users
   - Update developer documentation
   - Add XML comments to new interfaces and services

## Technical Details

### Azure Function Implementation
```csharp
// SaveSharedAnalysis
[FunctionName("SaveSharedAnalysis")]
public static async Task<IActionResult> SaveSharedAnalysis(
    [HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest req,
    [Blob("shared-analyses", Connection = "AzureWebJobsStorage")] BlobContainerClient containerClient,
    ILogger log)
{
    log.LogInformation("Processing shared analysis save request");
    
    string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
    var analysisData = JsonConvert.DeserializeObject<AnalysisData>(requestBody);
    
    // Generate unique ID
    string id = Guid.NewGuid().ToString();
    
    // Save to blob storage with the ID as the blob name
    var blobClient = containerClient.GetBlobClient($"{id}.json");
    using var ms = new MemoryStream(Encoding.UTF8.GetBytes(requestBody));
    await blobClient.UploadAsync(ms, overwrite: true);
    
    return new OkObjectResult(new { id });
}

// GetSharedAnalysis
[FunctionName("GetSharedAnalysis")]
public static async Task<IActionResult> GetSharedAnalysis(
    [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "shared/{id}")] HttpRequest req,
    string id,
    [Blob("shared-analyses/{id}.json", Connection = "AzureWebJobsStorage")] string analysisJson,
    ILogger log)
{
    log.LogInformation($"Retrieving shared analysis with ID: {id}");
    
    if (string.IsNullOrEmpty(analysisJson))
    {
        return new NotFoundResult();
    }
    
    return new OkObjectResult(analysisJson);
}
```

Shared Analysis History Model

```csharp
public record SharedAnalysisRecord
{
    public string Id { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public DateTime SharedDate { get; init; } = DateTime.UtcNow;
    public string SourceType { get; init; } = string.Empty;
    public string SourceId { get; init; } = string.Empty;
}
```

Client-Side Service

```csharp
public interface IAnalysisSharingService
{
    Task<string> ShareAnalysisAsync(AnalysisData analysis);
    Task<AnalysisData?> GetSharedAnalysisAsync(string id);
    Task<List<SharedAnalysisRecord>> GetSharedAnalysisHistoryAsync();
    Task SaveSharedAnalysisToHistoryAsync(SharedAnalysisRecord record);
}
```

Notes
* Consider implementing expiration for shared analyses to manage storage costs
* Evaluate rate limiting to prevent abuse of the sharing feature
* Ensure all shared data is stripped of any potentially sensitive information
* Consider analytics tracking for shared links to measure feature usage
* Evaluate GDPR implications of storing shared analysis data