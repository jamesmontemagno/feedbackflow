This page tracks the latest features and improvements added to FeedbackFlow.

## 📆 October 2025

### 🔍 Omni Search - Multi-Platform Content Discovery

The most requested feature is here! Search across all supported platforms simultaneously:

- 🌐 **Universal Search**: Search YouTube, Reddit, Hacker News, Twitter/X, and BlueSky all at once
- ⚡ **Blazing Fast**: Server-side aggregation with intelligent caching (results in seconds)
- 🎯 **Smart Filtering**: Filter by platform, comment count, text search, and more
- 📊 **Unified Results**: 100 results per platform with consistent formatting
- 🔄 **Real-time Sorting**: Sort by comments, engagement, recency, or oldest first
- 💬 **Comment-First**: Prioritizes content with actual discussions for better analysis
- 🎨 **Clean UI**: Beautiful result cards with platform badges and instant filtering
- 📱 **Mobile-Ready**: Fully responsive design works perfectly on all devices

### 📋 Enhanced Content Feed Selection

Supercharge your workflow with improved multi-source analysis:

- ☑️ **Cross-Platform Selection**: Select up to 10 items across all content sources
- 🎯 **Unified Sidebar**: Floating sidebar shows all selected items with quick actions
- 💾 **Persistent State**: Selections saved to localStorage and survive page refreshes
- 🔀 **Tab Caching**: Switch between sources without losing your search results
- ⚡ **Bulk Analysis**: Analyze multiple items at once with a single click
- 🎨 **Visual Feedback**: Selected items highlighted with clear visual indicators

### 🎨 Theme Improvements

Enhanced theming system for better user experience:

- 🌓 **System Default Option**: Three-way theme toggle (Light/Dark/System)
- 🎯 **Automatic Detection**: Respects your OS theme preference
- 🔄 **Dynamic Switching**: Seamlessly adapts to system theme changes
- 🎨 **Consistent Styling**: All components fully support both themes

### 🤖 Model Context Protocol (MCP) Server

FeedbackFlow now integrates with AI assistants through MCP:

- 🐳 **Docker Distribution**: Pre-built Docker images for instant deployment
- 🔌 **Tool Integration**: Expose FeedbackFlow features to MCP-compatible clients
- 🔐 **Secure Access**: API key-based authentication
- 📊 **Multiple Tools**: Auto-analyze, GitHub reports, Reddit reports, and more
- 💻 **Local & Remote**: Supports both stdio (local) and SSE (cloud) modes

## 📆 September 2025

### 🔧 Platform Search Enhancements

Improved search capabilities across all platforms:

- 🐦 **Enhanced Twitter Search**: Better query encoding for special characters (.NET, C#, etc.)
- 🦋 **BlueSky Updates**: Updated to latest AT Protocol API changes
- 📊 **Reddit Improvements**: More reliable post fetching and filtering
- 🎯 **Unified Service Layer**: Consistent search patterns across all platforms

### 💼 Multi-Select Content Feeds

Added powerful batch operations to Content Feeds:

- ☑️ **Checkbox Selection**: Select individual videos, posts, or stories
- 🎯 **Bulk Actions**: Analyze multiple items in one go
- 📋 **Visual Selection State**: Clear indicators for selected items
- ⚡ **Quick Access**: Streamlined workflow for content analysis

### 🔀 Individual Report Toggles

More control over automated reports:

- 🎚️ **Per-URL Control**: Enable/disable reports for individual URLs
- 📊 **Flexible Management**: Mix automated and manual report generation
- 💾 **Persistent Settings**: Report preferences saved per source

## 📆 August 2025

### 🍿 Analysis Improvements

- Custom prompts for each analysis request, use the universal, or customize it
- Reanalyze with custom prompts without having to waste credits re-gathering comments

### 🔐 Authentication & Cloud Backup

FeedbackFlow now offers secure user accounts with cloud synchronization:

- 👤 **User Authentication**: Create your personal FeedbackFlow account for enhanced features
- ☁️ **Cloud Backup**: Your analysis history automatically syncs across all your devices
- 🔄 **Seamless Migration**: Existing local data is preserved
- 🔒 **Secure Storage**: All data is stored securely in the cloud
- 📧 **Report Emails**: Wake up to weekly reports in your inbox

### 🎯 Tiered Account System

Choose the plan that fits your needs:

- 🆓 **Free**: Basic analysis features with usage limits
- ⭐ **Pro**: Enhanced capabilities with higher usage limits and priority processing
- 🏢 **Pro+**: Significantly higher usage limits with advanced features for teams and organizations

### 🌐 Enhanced Sharing System

Share your analysis with more control and flexibility:

- 🔓 **Public Sharing**: Make your analysis discoverable by the community
- 🔒 **Private Sharing**: Share with specific people using secure links
- 👁️ **Visibility Controls**: Choose who can view your shared analysis

### 💾 Data Continuity

Your existing data is safe and accessible:

- 🏠 **Local Data Preserved**: All your existing local analysis history remains available

## 📆 June 2025

### 📊 Custom Reports

We've added a new Report section to FeedbackFlow! You can now create your own requests for GitHub & Reddit that are generated each week.

- 📈 Advanced sentiment trend analysis over time
- 🔍 Deeper insight into user engagement patterns
- 📆 Updated automatically every week

### 🗃️ Export Analysis & Comments

- Export with 1 click to json, csv, pdf, or markdown!

### 🏠 Streamlined Home Experience

We've simplified the home page to make feedback analysis even easier:

- 🎯 Clean, focused URL input field
- ⚡ Quick-access buttons for recent analyses
- 🔄 Improved history organization
- 📱 Better mobile responsiveness

## 📅 May 2025

### 🔗 Share Analysis Feature

You can now easily share your analysis results with others:

- 🎯 Generate a unique shareable link for any analysis
- 👥 Recipients don't need a FeedbackFlow account to view shared analysis
- ✨ Shared analysis preserves all formatting and insights from the original

### ⚡ Auto Mode for Multiple Articles

Analyze multiple articles simultaneously with our new Auto Mode feature. Simply paste multiple URLs or content sources, and FeedbackFlow will process them all at once, providing a comprehensive summary and analysis.

- 🔄 Supports all content types (YouTube, GitHub, Reddit, Twitter, etc.)
- 📊 Creates a combined analysis across all provided sources

### 🔨 Optimizations

- Switched to single single URL entry to simplify flow
- YouTube playlist will output analysis of each individual video
- Switch to IndexedDB for saving history to improve performance and large summaries

## 📅 April 2025

### 🎉 Initial Release

- 💬 Basic feedback analysis for YouTube comments
- 🐙 Support for GitHub issues and discussions
- 🔵 Reddit thread analysis
- 🐦 Twitter/X post and reply analysis
- 💡 HackerNews discussion analysis
- ✍️ Manual input for custom content