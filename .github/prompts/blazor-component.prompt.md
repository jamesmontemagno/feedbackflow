# Blazor Component Creation Prompt

Use this prompt to generate a NEW Blazor component for the `feedbackwebapp` project that fully conforms to the repository's established patterns and best practices. Supply concrete values for all REQUIRED placeholders. Omit any optional section ONLY if it truly does not apply.

---
## INPUT TEMPLATE (Fill These Before Executing Prompt)

ComponentName: (PascalCase, no spaces, must end with a descriptive noun; e.g. `SharedHistoryList`, `GitHubIssueCard`)
FolderPath (relative to `feedbackwebapp/Components/`): (e.g. `Feedback/Results`, `Admin/Reports`, `Shared`)
ComponentPurpose (1–2 sentences):
KeyFeatures (bulleted):
PrimaryDataModel(s): (e.g. `CommentData`, `AnalysisData`, custom DTO)
Parameters:
- (Name: Type – required/optional – purpose – default if any)
ChildContentNeeded (yes/no + description if yes):
InjectedServices (DI): (e.g. `IExportService`, `IAuthenticationHeaderService`)
StateVariables (if any):
Events / Callbacks (EventCallback / Func):
SupportsStreaming (yes/no + how):
SupportsPagination / Virtualization (yes/no + approach):
ErrorHandlingNeeds (usage limit? transient? validation?):
LoadingStates (what do we show?):
EmptyStateMessage:
AccessibilityNotes:
ExampleUsage (Razor snippet calling this component):
RelatedStyles / ReusedClasses:
TestCoverageNeeded (yes/no + focus areas):

---
## OUTPUT REQUIREMENTS
Produce ALL of the following sections in order:
1. Summary
2. Files To Create
3. Component (.razor) Source
4. Component Code-Behind (.razor.cs) IF logic is non-trivial (skip if tiny)
5. Component Styles (.razor.css)
6. Optional Shared DTO / Model (only if new)
7. Example Usage Snippet
8. Minimal Test Skeleton (only if TestCoverageNeeded = yes)
9. Notes & Future Enhancements

Do NOT include extraneous commentary. Ensure namespaces, usings, and styling match repo conventions.

---
## GLOBAL CONSTRAINTS & CONVENTIONS

### Namespaces & Structure
- Razor file MUST begin with `@namespace FeedbackWebApp.Components.<FolderPath segments PascalCased>`
- Place file at: `feedbackwebapp/Components/<FolderPath>/<ComponentName>.razor`
- If code-behind used, create `<ComponentName>.razor.cs` adjacent with `partial class <ComponentName>`.
- Use `@using` only for namespaces actually referenced; order: System*, Microsoft*, then application.

### Parameters
- Use `[Parameter]` with `public` and PascalCase.
- Use `[EditorRequired]` for mandatory parameters.
- Use `<TItem>` generics only when necessary.
- Provide XML doc comments for public parameters (1 line, purpose + constraints).

### Dependency Injection
Inject services at top after `@code` or in code-behind via ctor.
Prefer code-behind if more than ~40 lines of C# logic.
Always use the existing services pattern & DI for HTTP or API calls.

### State & Lifecycle
- Favor `OnParametersSetAsync` for reacting to parameter changes.
- Use `CancellationTokenSource` for abortable async operations (dispose in `Dispose`).
- Use `InvokeAsync(StateHasChanged)` only when updating state from non-UI thread callbacks.
- Implement `IDisposable` or `IAsyncDisposable` if registering events or using CTS.

### Error Handling
Follow pattern:
```csharp
try
{
    // work
}
catch (Exception ex)
{
    _logger.LogError(ex, "<ComponentName>: <friendly context>");
    if (UsageLimitErrorHelper.TryParseUsageLimitError(ex.Message, out var limitError))
    {
        usageLimitError = limitError;
        showUsageLimitDialog = true;
    }
    else
    {
        errorMessage = $"An error occurred: {ex.Message}";
    }
}
```
Include `@using SharedDump.Utils` if using usage limit logic.
Display `UsageLimitDialog` when `showUsageLimitDialog` is true.

### UI & Markup
- Wrap main container in root element with class: `component-<kebab-name>` (e.g. `component-shared-history-list`).
- Use Bootstrap grid/utility classes for spacing (`mb-3`, `d-flex`, etc.).
- Use semantic HTML (`<section>`, `<header>`, `<nav>`, `<ul>`, `<article>` as applicable).
- Add ARIA attributes for interactive regions (`role="status"`, `aria-live="polite"` for streaming, etc.).
- Add loading skeleton or spinner using Bootstrap or lightweight shimmer.

### Styling (.razor.css)
- ONLY reference CSS variables: never hard-coded color literals.
- Root scope: `.component-<kebab-name>` to avoid leaking.
- Provide light/dark compatibility via `[data-theme="dark"] .component-<kebab-name>` overrides only where necessary.
- Use transitions: `transition: color .3s ease, background-color .3s ease, box-shadow .3s ease;`
- Use consistent radii: `border-radius: var(--border-radius);`
- Example variable patterns:
  - `color: var(--text-primary);`
  - `background: var(--card-bg);`
  - `box-shadow: var(--card-shadow);`
- Provide mobile breakpoint adjustments at bottom (max-width: 575.98px) when layout changes.

### Theming & Icons
- Use Bootstrap Icons: `<i class="bi bi-[icon]"></i>`.
- Add hover effects with `transform: translateY(-2px);` on actionable cards/buttons.

### Performance
- Use `@key` on list items.
- Use `<Virtualize>` when large datasets (configurable overscan).
- Debounce input events if filtering (250ms typical).

### Accessibility
- Ensure focus management when dialogs open.
- Provide `aria-label` or `title` on icon-only buttons.
- Maintain color contrast; rely on variables already tuned for both themes.

### Tests (When Requested)
Focus on:
- Parameter validation (throws or guards)
- Rendering decisions (loading/empty/error states)
- EventCallback invocation
- Streaming update logic (if applicable)
Use MSTest with pattern: `MethodName_Scenario_Expected()`.

---
## SECTION BLUEPRINTS

### 1. Summary
Short abstract: what the component does and why it exists.

### 2. Files To Create
List each file with relative path + 1-line purpose.

### 3. Component (.razor)
- Full markup
- Parameters
- UsageLimitDialog conditional
- Loading / Empty / Error regions
- Streaming region (if applicable)

### 4. Code-Behind (.razor.cs) (Conditional)
- Partial class
- DI constructor (if services used)
- Fields: logger, services, state, CTS
- Lifecycle methods
- Dispose pattern

### 5. Styles (.razor.css)
- Root-scoped selectors
- State modifiers: `.is-loading`, `.has-error`, `.is-empty`
- Animations (e.g. fadeIn) if used

### 6. Optional Shared DTO / Model
Only if new data shape required (goes in `shareddump/Models/...` with namespace `SharedDump.Models...`).

### 7. Example Usage Snippet
Embed in a parent page (e.g. inside another component or page).

### 8. Minimal Test Skeleton
Create test file path under `feedbackflow.tests/` with class named `<ComponentName>Tests`.

### 9. Notes & Future Enhancements
Bulleted backlog ideas; avoid scope creep.

---
## QUALITY CHECK BEFORE FINAL OUTPUT
Validate all of:
- Namespaces correct
- No unused usings
- No color literals
- Usage limit pattern correct if invoked
- Async methods use `await`
- Public parameters documented
- CSS only affects component via root class
- ARIA attributes present for dynamic content regions

---
## EXAMPLE (ABBREVIATED) KEBAB NAME TRANSFORMATION
`GitHubIssueCard` -> `component-git-hub-issue-card` (But prefer grouping natural words: `component-github-issue-card`). Break on casing boundaries.

---
## START GENERATION AFTER THIS LINE
(Replace everything below with the generated component package.)
