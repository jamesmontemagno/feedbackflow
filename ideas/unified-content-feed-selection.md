# Unified Content Feed Selection Plan

## Overview
Consolidate individual result component selection tracking into a parent-level unified selection management system with a 10-item cross-source limit, floating sidebar for selected items, and full state persistence across sessions.

## Design Decisions
- ✅ **10-item cross-source limit** - Prevents overwhelming analysis requests
- ✅ **Floating sidebar UI** - Collapsible panel on right side showing selected items
- ✅ **Clear selections only** - Keep form/results cached when switching tabs
- ✅ **Mixed-source analysis** - Support YouTube + Reddit + HN URLs together

## Implementation Tasks

### ✅ Completed
- [x] Plan documented
- [x] Created unified selection data model (SelectedItem record in ContentFeeds.razor)
- [x] Implemented 10-item selection limit enforcement
- [x] Created SelectedItemsSidebar component with floating UI
- [x] Added localStorage persistence for selections
- [x] Removed selection tracking from all result components
- [x] Updated result components to sync with parent state
- [x] Wired Analyze Selected button in sidebar
- [x] Added platform-specific selection handlers
- [x] Implemented tab state caching layer (SaveCurrentTabState/LoadTabState)
- [x] All result components updated (YouTube, Reddit, HackerNews, Omni)

### 📋 To Do
- [ ] Test cross-source selection and persistence
- [ ] Update documentation

## Data Structures

### SelectedItem Record
```csharp
record SelectedItem(
    string SourceType,    // "youtube", "reddit", "hackernews", "omni"
    string ItemId,        // Unique ID for the item
    string Title,         // Display title
    string Url,          // Full URL for analysis
    string PlatformBadge, // Badge text (e.g., "YouTube", "Reddit")
    string Icon          // Bootstrap icon class
);
```

### TabState Record
```csharp
record TabState(
    object? FormState,    // Form inputs (topic, subreddit, etc.)
    object? Results      // Fetched results (videos, threads, items)
);
```

### LocalStorage Keys
- `contentfeeds_selectedItems` - JSON array of SelectedItem
- `contentfeeds_tabState_youtube` - Form + results for YouTube
- `contentfeeds_tabState_reddit` - Form + results for Reddit
- `contentfeeds_tabState_hackernews` - Form + results for Hacker News
- `contentfeeds_tabState_omni` - Form + results for Omni Search

## Integration Points

### Parent State Management (ContentFeeds.razor)
```csharp
private List<SelectedItem> selectedItems = new();
private Dictionary<string, TabState> tabStateCache = new();
private const int MAX_SELECTIONS = 10;
```

### Result Component Interface
```csharp
[Parameter] public HashSet<string> ParentSelectedIds { get; set; }
[Parameter] public EventCallback<(string itemId, bool isSelected)> OnSelectionChanged { get; set; }
```

## Documentation Tasks
- [ ] Update `docs/` with content feed selection guide
- [ ] Document localStorage schema
- [ ] Add user guide for multi-source analysis workflow

## Status
**Created:** October 23, 2025
**Status:** ✅ Implementation Complete - Ready for Testing

## Summary of Implementation

All core features have been implemented successfully:

### ✅ Core Components Created
- **SelectedItemsSidebar.razor** - Floating collapsible sidebar with visual feedback
- **SelectedItemsSidebar.razor.css** - Responsive styling with dark theme support
- **ContentFeeds.razor** - Parent-level selection management with unified state

### ✅ Data Models Implemented
- `SelectedItem` record with source type, ID, title, URL, badge, and icon
- `TabState` record for caching form state and results across tab switches
- `selectedItems` List and `selectedItemIds` HashSet for efficient lookups

### ✅ Selection Management Features
- 10-item cross-source limit with enforcement
- localStorage persistence for selections across sessions
- Platform-specific selection handlers for YouTube, Reddit, HackerNews, and Omni
- Remove individual items and clear all functionality
- Auto-expand sidebar when items are selected

### ✅ Tab State Caching
- `SaveCurrentTabState()` - Saves results when switching tabs
- `LoadTabState()` - Restores cached results when returning to tabs
- Prevents loss of fetched data when exploring different sources

### ✅ Result Component Integration
All result components updated with:
- `ParentSelectedIds` parameter to sync checkbox state
- `OnSelectionChanged` callback with itemId, isSelected, and itemData
- Removed individual "Analyze Selected" buttons (consolidated in sidebar)
- Platform-specific ID prefixes (youtube_, reddit_, hackernews_, omni_)

### ✅ UI/UX Enhancements
- Collapsible sidebar with toggle button
- Visual selection highlighting on result cards
- Platform badges with color coding
- Analyze All button with loading state
- Empty state messaging
- Responsive design for mobile/tablet/desktop
