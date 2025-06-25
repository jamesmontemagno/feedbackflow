namespace SharedDump.Services.Mock;

/// <summary>
/// Provides standardized mock analysis content for different service types
/// </summary>
public static class MockAnalysisProvider
{
    /// <summary>
    /// Gets mock analysis content for a specific service type
    /// </summary>
    /// <param name="serviceType">The service type (github, youtube, reddit, etc.)</param>
    /// <param name="commentCount">Number of comments analyzed</param>
    /// <param name="customPrefix">Optional custom prefix for the analysis</param>
    /// <returns>Mock analysis content in markdown format</returns>
    public static string GetMockAnalysis(string serviceType, int commentCount = 0, string? customPrefix = null)
    {
        var analysis = serviceType.ToLowerInvariant() switch
        {
            "github" => GetGitHubAnalysis(commentCount),
            "youtube" => GetYouTubeAnalysis(commentCount),
            "reddit" => GetRedditAnalysis(commentCount),
            "hackernews" => GetHackerNewsAnalysis(commentCount),
            "devblogs" => GetDevBlogsAnalysis(commentCount),
            "twitter" => GetTwitterAnalysis(commentCount),
            "bluesky" => GetBlueSkyAnalysis(commentCount),
            "manual" => GetManualAnalysis(commentCount),
            _ => GetDefaultAnalysis(commentCount)
        };

        if (!string.IsNullOrEmpty(customPrefix))
        {
            analysis = $"{customPrefix}\n\n{analysis}";
        }

        return analysis;
    }

    private static string GetGitHubAnalysis(int commentCount)
    {
        return $"""
# GitHub Feedback Analysis 💻

## Overview
Total Comments Analyzed: {commentCount}
Overall Sentiment: Positive
Engagement Level: High

## Key Themes 🎯

### Technical Discussion
- **Bug Reports**: 35% of feedback relates to bug reports and issues
- **Feature Requests**: 28% are feature enhancement requests  
- **Documentation**: 22% concern documentation improvements
- **Performance**: 15% discuss performance optimizations

### Community Insights 👥
- **Positive**: 45% - Users appreciate the project and express gratitude
- **Neutral**: 35% - Factual reports and technical discussions
- **Negative**: 20% - Frustrations with bugs or missing features

## Top Issues 📊
1. Authentication problems mentioned in 12 comments
2. UI/UX improvements requested by 8 users
3. Performance issues reported in 6 threads

## Recommendations 💡
- Prioritize authentication fixes as they affect user experience significantly
- Consider UI/UX improvements for next release
- Address performance bottlenecks in high-traffic areas
- Improve documentation based on user feedback

## Action Items ✅
- [ ] Review authentication flow implementation
- [ ] Create UI/UX improvement backlog
- [ ] Performance profiling of critical paths
- [ ] Documentation audit and updates
""";
    }

    private static string GetYouTubeAnalysis(int commentCount)
    {
        return $"""
# YouTube Comments Analysis 🎬

## Overview
Total Comments Analyzed: {commentCount}
Average Sentiment: Very Positive (4.2/5.0)
Engagement Rate: High community involvement

## Content Themes 🎯

### Viewer Feedback
- **Tutorial Clarity**: 40% of comments about tutorial effectiveness
- **Technical Questions**: 30% asking for clarification or help
- **Appreciation**: 20% expressing thanks and positive feedback
- **Suggestions**: 10% offering improvement ideas

### Engagement Patterns 📊
- **Very Positive**: 32% - Enthusiastic responses and praise
- **Positive**: 28% - General satisfaction and approval
- **Neutral**: 25% - Questions and factual comments
- **Negative**: 15% - Criticism or confusion

## Popular Topics 🌟
1. Code examples and implementation details
2. Alternative approaches and best practices
3. Requests for follow-up videos
4. Platform-specific questions

## Community Insights 💬
- High engagement indicates strong community interest
- Many viewers are actively applying the content
- Demand for more advanced topics and deep-dives
- Strong appreciation for clear explanations

## Recommendations 💡
- Create follow-up content addressing common questions
- Consider advanced tutorial series
- Improve code example clarity where mentioned
- Engage more with community questions in replies
""";
    }

    private static string GetRedditAnalysis(int commentCount)
    {
        return $"""
# Reddit Discussion Analysis 🔷

## Overview
Total Comments Analyzed: {commentCount}
Discussion Quality: High-quality technical conversations
Community Health: Strong and supportive

## Discussion Patterns 📊

### Content Distribution
- **Technical Discussions**: 45% - In-depth technical conversations
- **Troubleshooting**: 25% - Users helping each other solve problems
- **Experience Sharing**: 20% - Users sharing implementations and experiences
- **Meta Discussion**: 10% - About the community and platform itself

### Sentiment Breakdown 💭
- **Constructive**: 55% - Helpful and informative contributions
- **Neutral**: 30% - Factual statements and questions
- **Critical**: 15% - Constructive criticism and concerns

## Community Insights 👥
- Strong community support and knowledge sharing
- Users appreciate detailed technical explanations
- Active problem-solving collaboration
- Interest in real-world implementation examples

## Trending Topics 🔥
- Best practices and design patterns
- Performance optimization techniques
- Framework comparisons and evaluations
- Tool recommendations and reviews

## Recommendations 💡
- Continue focus on technical depth and accuracy
- Encourage more experience sharing
- Foster the supportive community environment
- Consider creating beginner-friendly content alongside advanced topics
""";
    }

    private static string GetHackerNewsAnalysis(int commentCount)
    {
        return $"""
# Hacker News Discussion Analysis 🔸

## Overview
Total Comments Analyzed: {commentCount}
Discussion Quality: High technical depth
Community Response: Engaged and analytical

## Discussion Themes 🎯

### Technical Focus
- **Architecture**: 40% - System design and scalability discussions
- **Implementation**: 25% - Specific technical approaches
- **Industry Impact**: 20% - Business and market implications
- **Alternative Solutions**: 15% - Competing approaches and tools

### Community Engagement 📊
- **Technical Depth**: High-quality technical discussions
- **Diverse Perspectives**: Multiple viewpoints and approaches
- **Industry Experience**: Comments from experienced practitioners
- **Critical Analysis**: Thoughtful evaluation of ideas

## Key Insights 💡
- Stories reaching front page indicate strong community interest
- Long discussion threads suggest engaging content
- High upvote ratios show content quality
- Cross-references to other projects and resources

## Notable Themes 🌟
1. Scalability and architecture discussions
2. Security considerations and best practices
3. Developer experience and tooling
4. Industry trends and future predictions

## Recommendations 📈
- Continue technical depth and accuracy
- Engage with constructive criticism
- Share implementation details and lessons learned
- Consider community feedback for future development
""";
    }

    private static string GetDevBlogsAnalysis(int commentCount)
    {
        return $"""
# Developer Blog Analysis 💻

## Overview
Total Comments Analyzed: {commentCount}
Reader Engagement: Active and thoughtful
Content Reception: Positive with constructive feedback

## Engagement Metrics 📊

### Comment Distribution
- **Technical Questions**: 35% - Clarification and implementation details
- **Appreciation**: 30% - Thanks and positive feedback
- **Experience Sharing**: 20% - Related experiences and examples
- **Corrections/Suggestions**: 15% - Improvements and error reports

### Reader Demographics 👥
- **Experience Level**: Mixed from beginners to seniors
- **Geographic Distribution**: Global audience
- **Technology Focus**: Primarily .NET and web technologies
- **Industry Sectors**: Varied across different domains

## Content Insights 💡
- Readers appreciate practical, actionable content
- Step-by-step tutorials perform well
- Code examples are highly valued
- Real-world applications resonate strongly

## Popular Elements 🌟
1. Modern development practices and patterns
2. Framework updates and new features
3. Performance optimization techniques
4. Developer productivity and tooling

## Recommendations 📈
- Continue focus on practical, hands-on content
- Increase tutorial and example-driven posts
- Engage more with reader questions and feedback
- Consider series format for complex topics
""";
    }

    private static string GetTwitterAnalysis(int commentCount)
    {
        return $"""
# Twitter/X Conversation Analysis 🐦

## Overview
Total Interactions: {commentCount}
Engagement Level: High
Overall Sentiment: Positive

## Engagement Patterns 📊

### Interaction Types
- **Replies**: 45% - Direct responses and conversations
- **Quote Tweets**: 25% - Shared with commentary
- **Likes/Retweets**: 20% - Simple engagement
- **Mentions**: 10% - Referenced in other discussions

### Conversation Dynamics 💬
- **Supportive**: 50% - Positive and encouraging responses
- **Informational**: 30% - Questions and factual exchanges
- **Critical**: 20% - Constructive criticism and debate

## Community Response 🌟
- High engagement indicates strong interest
- Cross-platform sharing extends reach
- Industry professionals participating
- Good balance of technical and general audience

## Key Themes 🎯
1. Feature announcements and updates
2. Technical implementation discussions
3. User experience and feedback
4. Community building and support

## Recommendations 💡
- Continue regular engagement with replies
- Share behind-the-scenes development insights
- Highlight community success stories
- Use threads for more detailed technical discussions
""";
    }

    private static string GetBlueSkyAnalysis(int commentCount)
    {
        return $"""
# BlueSky Discussion Analysis 🦋

## Overview
Total Interactions: {commentCount}
Engagement Level: High
Platform Dynamics: Emerging community patterns

## Community Engagement 📊

### Interaction Patterns
- **Direct Responses**: 40% - Thoughtful replies and discussions
- **Community Building**: 30% - Relationship and network building
- **Technical Discussion**: 20% - Feature and implementation talk
- **Platform Exploration**: 10% - Learning BlueSky-specific features

### Sentiment Distribution 💙
- **Enthusiastic**: 45% - Excitement about platform and features
- **Curious**: 35% - Questions and exploration
- **Supportive**: 20% - Community support and encouragement

## Platform Insights 🌟
- Early adopter community with high engagement
- Technical audience interested in platform features
- Strong community support and collaboration
- Growing interest in decentralized features

## Unique BlueSky Elements 🚀
1. Decentralization and protocol discussions
2. Custom feed and algorithm interest
3. Data portability and user control
4. Community moderation and governance

## Recommendations 💡
- Engage actively with the growing community
- Share insights about BlueSky-specific features
- Highlight decentralization benefits
- Build relationships within the emerging ecosystem
""";
    }

    private static string GetManualAnalysis(int commentCount)
    {
        return $"""
# Content Analysis Summary 📑

## Overview
Total Content Analyzed: {commentCount} items
Analysis Type: Manual content review
Content Quality: Mixed to high

## Key Findings 🎯

### Content Themes
- **Primary Topics**: Core subject matter and main themes
- **Supporting Elements**: Secondary topics and related content
- **Engagement Indicators**: Interaction and response patterns
- **Quality Markers**: Depth, accuracy, and value indicators

### Insights Summary 💡
- **Strengths**: Well-structured content with clear messaging
- **Opportunities**: Areas for improvement and expansion
- **Patterns**: Recurring themes and consistent elements
- **Recommendations**: Actionable next steps

## Content Assessment 📊

### Quality Metrics
- **Clarity**: Information presented clearly and understandably
- **Relevance**: Content aligns with intended purpose
- **Depth**: Appropriate level of detail for audience
- **Accuracy**: Factual correctness and current information

### Engagement Factors 🌟
1. Clear structure and organization
2. Actionable insights and recommendations
3. Balanced perspective and comprehensive coverage
4. Relevant examples and practical applications

## Strategic Recommendations 📈
- Focus on high-performing content themes
- Address identified gaps and opportunities
- Maintain consistent quality standards
- Consider audience feedback for future content
""";
    }

    private static string GetDefaultAnalysis(int commentCount)
    {
        return $"""
# Feedback Analysis Summary 📊

## Overview
Total Items Analyzed: {commentCount}
Analysis Type: General feedback review
Overall Assessment: Positive engagement

## Key Patterns 🎯

### Feedback Categories
- **Feature Requests**: Most common type of feedback
- **Bug Reports**: Critical for product improvement
- **User Experience**: Important for adoption and retention
- **Documentation**: Essential for user success

### Sentiment Distribution 💭
- **Positive**: 58% - Users express satisfaction and appreciation
- **Neutral**: 27% - Factual comments and questions
- **Negative**: 15% - Concerns and improvement suggestions

## Success Indicators 🌟
1. Growing community engagement
2. Constructive feedback and suggestions
3. Active problem-solving collaboration
4. Positive sentiment trends

## Actionable Insights 💡
- Prioritize user-reported issues and suggestions
- Maintain active community engagement
- Focus on documentation quality and completeness
- Continue iterative improvement based on feedback

## Recommendations 📈
- Address high-priority feedback items
- Engage regularly with the community
- Share development progress and updates
- Celebrate community contributions and successes
""";
    }
}
