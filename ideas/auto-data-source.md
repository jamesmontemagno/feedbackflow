# Idea: Auto Data Source for Multi-URL Input and Sentiment Analysis

## Description
Implement a new "auto" data source mode in the Blazor web app that allows users to input multiple URLs, dynamically add/remove them, and submit for analysis. On submission, the app should:
- Parse each URL to determine its type (YouTube, Reddit, etc.).
- Use the appropriate service to fetch all comments for each URL.
- Aggregate all comments and perform sentiment analysis.
- Display the sentiment results in the UI.

## Acceptance Criteria
- [ ] New Blazor component for "auto" mode with dynamic URL input list.
- [ ] URL parsing logic to determine service type.
- [ ] Service selection and comment aggregation for all URLs.
- [ ] Sentiment analysis on aggregated comments.
- [ ] User-friendly, accessible UI with error handling.
- [ ] Component-specific CSS, respecting light/dark themes.
- [ ] Unit tests for URL parsing and service selection.

## Implementation Plan
1. **Component/UI**
   - Create `AutoDataSource.razor` and `AutoDataSource.razor.css` in the appropriate feature folder.
   - Implement a dynamic list of URL input fields with add/remove buttons.
   - Add a submit button to trigger analysis.

2. **URL Parsing**
   - Use or extend existing URL parsing utilities (from `shareddump`).
   - For each URL, determine its type (YouTube, Reddit, etc.).

3. **Service Selection & Data Gathering**
   - For each URL, resolve the correct service via dependency injection (e.g., `IYouTubeService`, `IRedditService`).
   - Fetch comments for each URL asynchronously and aggregate results.

4. **Sentiment Analysis**
   - Pass all aggregated comments to the sentiment analysis service.
   - Collect and display sentiment results in the UI.

5. **Error Handling & UX**
   - Show loading, error, and empty states.
   - Validate URLs and provide user feedback for invalid entries.

6. **Styling & Accessibility**
   - Use semantic HTML and ARIA attributes as needed.
   - Ensure keyboard navigation and accessibility.
   - Use only theme variables for colors (no hardcoded values).

7. **Testing**
   - Add/extend MSTest unit tests for URL parsing and service selection logic in `feedbackflow.tests`.

8. **Documentation**
   - Document the new component and any new services or interfaces.

## Notes
- Use dependency injection and interfaces for extensibility.
- Reuse or extend existing logic in `shareddump` where possible.
- Follow project coding and accessibility guidelines.
