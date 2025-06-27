using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using SharedDump.Json;
using SharedDump.Services.Interfaces;

namespace SharedDump.Models.GitHub;

public class GitHubService : IGitHubService
{
    private readonly HttpClient _client;
    private readonly string _accessToken;
    private readonly int _maxRetries = 5;

    public GitHubService(string accessToken, HttpClient client)
    {
        _accessToken = accessToken ?? throw new ArgumentNullException(nameof(accessToken));
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _client.DefaultRequestHeaders.Authorization = new("Bearer", accessToken);
        _client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("ghdump", "1.0.0"));
    }

    public async Task<bool> CheckRepositoryValid(string repoOwner, string repoName)
    {
        var checkRepoQuery = @"
        query($owner: String!, $name: String!) {
            repository(owner: $owner, name: $name) {
                id
            }
        }";

        var response = await _client.PostAsJsonAsync("https://api.github.com/graphql", new GithubRepositoryQuery
        {
            Query = checkRepoQuery,
            Variables = new() { Owner = repoOwner, Name = repoName }
        },
        GithubApiJsonContext.Default.GithubRepositoryQuery);

        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine(await response.Content.ReadAsStringAsync());
            return false;
        }

        var result = await response.Content.ReadFromJsonAsync(GithubApiJsonContext.Default.GraphqlResponse);
        return result?.Data.Repository is not null;
    }

    public async Task<List<GithubDiscussionModel>> GetDiscussionsAsync(string repoOwner, string repoName)
    {
        var discussionsList = new List<GithubDiscussionModel>();
        var hasMorePages = true;
        string? endCursor = null;
        var retryCount = 0;

        while (hasMorePages)
        {
            var discussionsQuery = @"
            query($owner: String!, $name: String!, $after: String) {
                repository(owner: $owner, name: $name) {
                    discussions(first: 100, after: $after) {
                        edges {
                            node {
                                id
                                title
                                url
                                answer {
                                    id
                                }
                                comments(first:100) {
                                    edges {
                                        node {
                                            id
                                            body
                                            url
                                            createdAt
                                            author {
                                                login
                                            }
                                            replies(first:100) {
                                                edges {
                                                    node {
                                                        id
                                                        body
                                                        url
                                                        createdAt
                                                        author {
                                                            login
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        pageInfo {
                            hasNextPage
                            endCursor
                        }
                    }
                }
            }";

            while (retryCount < _maxRetries)
            {
                var response = await _client.PostAsJsonAsync("https://api.github.com/graphql", new GithubDiscussionQuery
                {
                    Query = discussionsQuery,
                    Variables = new() { Owner = repoOwner, Name = repoName, After = endCursor }
                },
                GithubApiJsonContext.Default.GithubDiscussionQuery);

                if (!response.IsSuccessStatusCode)
                {
                    await HandleRateLimit(response);
                    retryCount++;
                    continue;
                }

                var result = await response.Content.ReadFromJsonAsync(GithubApiJsonContext.Default.GraphqlResponse);

                if (result?.Data.Repository?.Discussions is not { } discussions)
                {
                    break;
                }

                foreach (var discussionEdge in discussions.Edges)
                {
                    var discussion = discussionEdge.Node;
                    var commentsList = new List<GithubCommentModel>();

                    foreach (var commentEdge in discussion.Comments.Edges)
                    {
                        var comment = commentEdge.Node;

                        var commentJson = new GithubCommentModel
                        {
                            Id = comment.Id,
                            Author = comment.Author?.Login ?? "??",
                            Content = comment.Body,
                            CreatedAt = comment.CreatedAt,
                            Url = comment.Url
                        };

                        commentsList.Add(commentJson);

                        if (comment.Replies is { } replies)
                        {
                            // Hoist replies as top-level comments
                            foreach (var replyEdge in replies.Edges)
                            {
                                var reply = replyEdge.Node;
                                var replyJson = new GithubCommentModel
                                {
                                    Id = reply.Id,
                                    ParentId = comment.Id,
                                    Author = reply.Author?.Login ?? "??",
                                    Content = reply.Body,
                                    CreatedAt = reply.CreatedAt,
                                    Url = reply.Url
                                };
                                commentsList.Add(replyJson);
                            }
                        }
                    }

                    var discussionJson = new GithubDiscussionModel
                    {
                        Title = discussion.Title,
                        AnswerId = discussion.Answer?.Id,
                        Url = discussion.Url,
                        Comments = commentsList.ToArray()
                    };

                    discussionsList.Add(discussionJson);
                }

                hasMorePages = discussions.PageInfo.HasNextPage;
                endCursor = discussions.PageInfo.EndCursor;

                if (!hasMorePages)
                {
                    break;
                }

                retryCount = 0;
            }

            if (retryCount == _maxRetries)
            {
                throw new Exception("Max retries exceeded");
            }
        }

        return discussionsList;
    }

    public async Task<List<GithubIssueModel>> GetIssuesAsync(string repoOwner, string repoName, string[] labels)
    {
        var issuesList = new List<GithubIssueModel>();
        var hasMorePages = true;
        string? endCursor = null;
        var retryCount = 0;

        while (hasMorePages)
        {
            var issuesQuery = @"
            query($owner: String!, $name: String!, $after: String, $labels: [String!]) {
                repository(owner: $owner, name: $name) {
                    issues(first: 100, after: $after, labels: $labels) {
                        edges {
                            node {
                                id
                                author {
                                    login
                                }
                                title
                                body
                                url
                                createdAt
                                updatedAt
                                reactions {
                                    totalCount
                                }
                                labels(first: 100) {
                                    nodes {
                                        name
                                    }
                                }
                                comments(first: 100) {
                                    edges {
                                        node {
                                            id
                                            body
                                            url
                                            createdAt
                                            author {
                                                login
                                            }                                            
                                        }
                                    }
                                }
                            }
                        }
                        pageInfo {
                            hasNextPage
                            endCursor
                        }
                    }
                }
            }";

            while (retryCount < _maxRetries)
            {
                var response = await _client.PostAsJsonAsync("https://api.github.com/graphql", new GithubIssueQuery
                {
                    Query = issuesQuery,
                    Variables = new() { Owner = repoOwner, Name = repoName, After = endCursor, Labels = labels }
                },
                GithubApiJsonContext.Default.GithubIssueQuery);

                if (!response.IsSuccessStatusCode)
                {
                    await HandleRateLimit(response);
                    retryCount++;
                    continue;
                }

                var result = await response.Content.ReadFromJsonAsync(GithubApiJsonContext.Default.GraphqlResponse);

                if (result?.Data.Repository?.Issues is not { } issues)
                {
                    break;
                }

                foreach (var issueEdge in issues.Edges)
                {
                    var issue = issueEdge.Node;
                    var commentsList = new List<GithubCommentModel>();

                    foreach (var commentEdge in issue.Comments.Edges)
                    {
                        var comment = commentEdge.Node;

                        var commentJson = new GithubCommentModel
                        {
                            Id = comment.Id,
                            Author = comment.Author?.Login ?? "??",
                            Content = comment.Body,
                            CreatedAt = comment.CreatedAt,
                            Url = comment.Url
                        };

                        commentsList.Add(commentJson);

                        if (comment.Replies is { } replies)
                        {
                            // Hoist replies as top-level comments
                            foreach (var replyEdge in replies.Edges)
                            {
                                var reply = replyEdge.Node;
                                var replyJson = new GithubCommentModel
                                {
                                    Id = reply.Id,
                                    ParentId = comment.Id,
                                    Author = reply.Author?.Login ?? "??",
                                    Content = reply.Body,
                                    CreatedAt = reply.CreatedAt,
                                    Url = reply.Url
                                };
                                commentsList.Add(replyJson);
                            }
                        }
                    }

                    var issueJson = new GithubIssueModel
                    {
                        Id = issue.Id,
                        Author = issue.Author?.Login ?? "??",
                        Title = issue.Title,
                        Body = issue.Body,
                        URL = issue.Url,
                        CreatedAt = DateTime.Parse(issue.CreatedAt),
                        LastUpdated = DateTime.Parse(issue.UpdatedAt),
                        Upvotes = issue.Reactions.TotalCount,
                        Labels = issue.Labels.Nodes.Select(l => l.Name),
                        Comments = commentsList.ToArray()
                    };

                    issuesList.Add(issueJson);
                }

                hasMorePages = issues.PageInfo.HasNextPage;
                endCursor = issues.PageInfo.EndCursor;

                if (!hasMorePages)
                {
                    break;
                }

                retryCount = 0;
            }

            if (retryCount == _maxRetries)
            {
                throw new Exception("Max retries exceeded");
            }
        }

        return issuesList;
    }

    public async Task<List<GithubIssueModel>> GetPullRequestsAsync(string repoOwner, string repoName, string[] labels)
    {
        var pullsList = new List<GithubIssueModel>();
        var hasMorePages = true;
        string? endCursor = null;
        var retryCount = 0;

        while (hasMorePages)
        {
            var pullsQuery = @"
            query($owner: String!, $name: String!, $after: String, $labels: [String!]) {
                repository(owner: $owner, name: $name) {
                    pullRequests(first: 100, after: $after, labels: $labels) {
                        edges {
                            node {
                                id
                                author {
                                    login
                                }
                                title
                                body
                                url
                                createdAt
                                updatedAt
                                reactions {
                                    totalCount
                                }
                                labels(first: 100) {
                                    nodes {
                                        name
                                    }
                                }
                                comments(first: 100) {
                                    edges {
                                        node {
                                            id
                                            body
                                            url
                                            createdAt
                                            author {
                                                login
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        pageInfo {
                            hasNextPage
                            endCursor
                        }
                    }
                }
            }";

            while (retryCount < _maxRetries)
            {
                var response = await _client.PostAsJsonAsync("https://api.github.com/graphql", new GithubIssueQuery
                {
                    Query = pullsQuery,
                    Variables = new() { Owner = repoOwner, Name = repoName, After = endCursor, Labels = labels }
                },
                GithubApiJsonContext.Default.GithubIssueQuery);

                if (!response.IsSuccessStatusCode)
                {
                    await HandleRateLimit(response);
                    retryCount++;
                    continue;
                }

                var result = await response.Content.ReadFromJsonAsync(GithubApiJsonContext.Default.GraphqlResponse);

                if (result?.Data.Repository?.PullRequests is not { } pulls)
                {
                    break;
                }

                foreach (var pullEdge in pulls.Edges)
                {
                    var pull = pullEdge.Node;
                    var commentsList = new List<GithubCommentModel>();

                    foreach (var commentEdge in pull.Comments.Edges)
                    {
                        var comment = commentEdge.Node;

                        var commentJson = new GithubCommentModel
                        {
                            Id = comment.Id,
                            Author = comment.Author?.Login ?? "??",
                            Content = comment.Body,
                            CreatedAt = comment.CreatedAt,
                            Url = comment.Url
                        };

                        commentsList.Add(commentJson);

                        if (comment.Replies is { } replies)
                        {
                            // Hoist replies as top-level comments
                            foreach (var replyEdge in replies.Edges)
                            {
                                var reply = replyEdge.Node;
                                var replyJson = new GithubCommentModel
                                {
                                    Id = reply.Id,
                                    ParentId = comment.Id,
                                    Author = reply.Author?.Login ?? "??",
                                    Content = reply.Body,
                                    CreatedAt = reply.CreatedAt,
                                    Url = reply.Url
                                };
                                commentsList.Add(replyJson);
                            }
                        }
                    }

                    var pullJson = new GithubIssueModel
                    {
                        Id = pull.Id,
                        Author = pull.Author?.Login ?? "??",
                        Title = pull.Title,
                        Body = pull.Body,
                        URL = pull.Url,
                        CreatedAt = DateTime.Parse(pull.CreatedAt),
                        LastUpdated = DateTime.Parse(pull.UpdatedAt),
                        Upvotes = pull.Reactions.TotalCount,
                        Labels = pull.Labels.Nodes.Select(l => l.Name),
                        Comments = commentsList.ToArray()
                    };

                    pullsList.Add(pullJson);
                }

                hasMorePages = pulls.PageInfo.HasNextPage;
                endCursor = pulls.PageInfo.EndCursor;

                if (!hasMorePages)
                {
                    break;
                }

                retryCount = 0;
            }

            if (retryCount == _maxRetries)
            {
                throw new Exception("Max retries exceeded");
            }
        }

        return pullsList;
    }

    /// <summary>
    /// Gets comments for a specific GitHub issue by its number
    /// </summary>
    /// <param name="repoOwner">Repository owner</param>
    /// <param name="repoName">Repository name</param>
    /// <param name="issueNumber">Issue number</param>
    /// <returns>List of comments for the specified issue</returns>
    public async Task<List<GithubCommentModel>> GetIssueCommentsAsync(string repoOwner, string repoName, int issueNumber)
    {
        var commentsList = new List<GithubCommentModel>();
        var hasMorePages = true;
        string? endCursor = null;
        var retryCount = 0;

        while (hasMorePages)
        {
            var issueQuery = @"
            query($owner: String!, $name: String!, $after: String, $issueNumber: Int!) {
                repository(owner: $owner, name: $name) {
                    issue(number: $issueNumber) {
                        id
                        author {
                            login
                        }
                        title
                        body
                        url
                        createdAt
                        updatedAt
                        reactions(content: THUMBS_UP) {
                            totalCount
                        }
                        labels(first: 100) {
                            nodes {
                                name
                            }
                        }
                        comments(first: 100, after: $after) {
                            edges {
                                node {
                                    id
                                    body
                                    url
                                    createdAt
                                    author {
                                        login
                                    }                                
                                }
                            }
                            pageInfo {
                                hasNextPage
                                endCursor
                            }
                        }
                    }
                }
            }";

            while (retryCount < _maxRetries)
            {
                var response = await _client.PostAsJsonAsync("https://api.github.com/graphql", new
                {
                    Query = issueQuery,
                    Variables = new { owner = repoOwner, name = repoName, after = endCursor, issueNumber }
                });

                if (!response.IsSuccessStatusCode)
                {
                    await HandleRateLimit(response);
                    retryCount++;
                    continue;
                }

                var result = await response.Content.ReadFromJsonAsync<GraphqlResponse>();

                var issue = result?.Data.Repository?.Issue;

                if (issue == null)
                {
                    break;
                }

                foreach (var commentEdge in issue.Comments.Edges)
                {
                    var comment = commentEdge.Node;

                    var commentJson = new GithubCommentModel
                    {
                        Id = comment.Id,
                        Author = comment.Author?.Login ?? "??",
                        Content = comment.Body,
                        CreatedAt = comment.CreatedAt,
                        Url = comment.Url
                    };

                    commentsList.Add(commentJson);

                    if (comment.Replies is { } replies)
                    {
                        // Hoist replies as top-level comments
                        foreach (var replyEdge in replies.Edges)
                        {
                            var reply = replyEdge.Node;
                            var replyJson = new GithubCommentModel
                            {
                                Id = reply.Id,
                                ParentId = comment.Id,
                                Author = reply.Author?.Login ?? "??",
                                Content = reply.Body,
                                CreatedAt = reply.CreatedAt,
                                Url = reply.Url
                            };
                            commentsList.Add(replyJson);
                        }
                    }
                }

                hasMorePages = issue.Comments?.PageInfo?.HasNextPage ?? false;
                endCursor = issue.Comments?.PageInfo?.EndCursor;

                if (!hasMorePages)
                {
                    break;
                }

                retryCount = 0;
            }

            if (retryCount == _maxRetries)
            {
                throw new Exception("Max retries exceeded");
            }
        }

        return commentsList;
    }

    /// <summary>
    /// Gets comments for a specific GitHub pull request by its number
    /// </summary>
    /// <param name="repoOwner">Repository owner</param>
    /// <param name="repoName">Repository name</param>
    /// <param name="pullNumber">Pull request number</param>
    /// <returns>List of comments for the specified pull request</returns>    
    public async Task<List<GithubCommentModel>> GetPullRequestCommentsAsync(string repoOwner, string repoName, int pullNumber)
    {
        var commentsList = new List<GithubCommentModel>();
        var hasMorePages = true;
        string? endCursor = null;
        var retryCount = 0;

        while(hasMorePages)
        {
            var pullQuery = @"
            query($owner: String!, $name: String!, $after: String, $pullNumber: Int!) {
                repository(owner: $owner, name: $name) {
                    pullRequest(number: $pullNumber) {
                        id
                        author {
                            login
                        }
                        title
                        body
                        url
                        createdAt
                        updatedAt
                        reactions(content: THUMBS_UP) {
                            totalCount
                        }
                        labels(first: 100) {
                            nodes {
                                name
                            }
                        }
                        comments(first: 100, after: $after) {
                            edges {
                                node {
                                    id
                                    body
                                    url
                                    createdAt
                                    author {
                                        login
                                    }                               
                                }
                            }
                            pageInfo {
                                hasNextPage
                                endCursor
                            }
                        }
                        reviews(first: 100) {
                            nodes {
                                author {
                                    login
                                }
                                body
                                comments(first: 100) {
                                    nodes {
                                        id
                                        body
                                        path
                                        position
                                        diffHunk
                                        url
                                        createdAt
                                        author {
                                            login
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }";

            while (retryCount < _maxRetries)
            {
                var response = await _client.PostAsJsonAsync("https://api.github.com/graphql", new
                {
                    Query = pullQuery,
                    Variables = new { owner = repoOwner, name = repoName, after = endCursor, pullNumber }
                });

                if (!response.IsSuccessStatusCode)
                {
                    await HandleRateLimit(response);
                    retryCount++;
                    continue;
                }

                var result = await response.Content.ReadFromJsonAsync<GraphqlResponse>();

                var pullRequest = result?.Data.Repository?.PullRequest;

                if (pullRequest == null)
                {
                    break;
                }

                foreach (var commentEdge in pullRequest.Comments.Edges)
                {
                    var comment = commentEdge.Node;

                    var commentJson = new GithubCommentModel
                    {
                        Id = comment.Id,
                        Author = comment.Author?.Login ?? "??",
                        Content = comment.Body,
                        CreatedAt = comment.CreatedAt,
                        Url = comment.Url
                    };

                    commentsList.Add(commentJson);

                    if (comment.Replies is { } replies)
                    {
                        // Hoist replies as top-level comments
                        foreach (var replyEdge in replies.Edges)
                        {
                            var reply = replyEdge.Node;                            
                            var replyJson = new GithubCommentModel
                            {
                                Id = reply.Id,
                                ParentId = comment.Id,
                                Author = reply.Author?.Login ?? "??",
                                Content = reply.Body,
                                CreatedAt = reply.CreatedAt,
                                Url = reply.Url
                            };
                            commentsList.Add(replyJson);
                        }
                    }
                }

                // Process review comments (code comments)
                if (pullRequest.Reviews?.Nodes != null)
                {
                    foreach (var review in pullRequest.Reviews.Nodes)
                    {
                        if (review.Comments?.Nodes == null)
                        {
                            continue;
                        }

                        foreach (var reviewComment in review.Comments.Nodes)
                        {
                            var reviewCommentJson = new GithubCommentModel
                            {
                                Id = reviewComment.Id,
                                Author = reviewComment.Author?.Login ?? "??",
                                Content = reviewComment.Body,
                                CreatedAt = reviewComment.CreatedAt,
                                Url = reviewComment.Url,
                                CodeContext = reviewComment.DiffHunk,
                                FilePath = reviewComment.Path,
                                LinePosition = reviewComment.Position
                            };

                            commentsList.Add(reviewCommentJson);
                        }
                    }
                }

                hasMorePages = pullRequest.Comments?.PageInfo?.HasNextPage ?? false;
                endCursor = pullRequest.Comments?.PageInfo?.EndCursor;

                if (!hasMorePages)
                {
                    break;
                }

                retryCount = 0;
            }

            if (retryCount == _maxRetries)
            {
                throw new Exception("Max retries exceeded");
            }
        }

        return commentsList;
    }

    /// <summary>
    /// Gets comments for a specific GitHub discussion by its number
    /// </summary>
    /// <param name="repoOwner">Repository owner</param>
    /// <param name="repoName">Repository name</param>
    /// <param name="discussionNumber">Discussion number</param>
    /// <returns>List of comments for the specified discussion</returns>
    public async Task<List<GithubCommentModel>> GetDiscussionCommentsAsync(string repoOwner, string repoName, int discussionNumber)
    {
        var commentsList = new List<GithubCommentModel>();
        var hasMorePages = true;
        string? endCursor = null;
        var retryCount = 0;

        while (hasMorePages)
        {
            var discussionQuery = @"
            query($owner: String!, $name: String!, $after: String, $discussionNumber: Int!) {
                repository(owner: $owner, name: $name) {
                    discussion(number: $discussionNumber) {
                        id
                        title
                        url
                        createdAt
                        updatedAt
                        answer {
                            id
                        }
                        comments(first: 100, after: $after) {
                            edges {
                                node {
                                    id
                                    body
                                    url
                                    createdAt
                                    author {
                                        login
                                    }
                                    replies(first: 100) {
                                        edges {
                                            node {
                                                id
                                                body
                                                url
                                                createdAt
                                                author {
                                                    login
                                                }
                                            }
                                        }
                                        pageInfo {
                                            hasNextPage
                                            endCursor
                                        }
                                    }
                                }
                            }
                            pageInfo {
                                hasNextPage
                                endCursor
                            }
                        }
                    }
                }
            }";

            while (retryCount < _maxRetries)
            {
                var response = await _client.PostAsJsonAsync("https://api.github.com/graphql", new
                {
                    Query = discussionQuery,
                    Variables = new { owner = repoOwner, name = repoName, after = endCursor, discussionNumber }
                });

                if (!response.IsSuccessStatusCode)
                {
                    await HandleRateLimit(response);
                    retryCount++;
                    continue;
                }

                var result = await response.Content.ReadFromJsonAsync<GraphqlResponse>();

                var discussion = result?.Data.Repository?.Discussion;

                if (discussion == null)
                {
                    break;
                }

                foreach (var commentEdge in discussion.Comments.Edges)
                {
                    var comment = commentEdge.Node;

                    var commentJson = new GithubCommentModel
                    {
                        Id = comment.Id,
                        Author = comment.Author?.Login ?? "??",
                        Content = comment.Body,
                        CreatedAt = comment.CreatedAt,
                        Url = comment.Url
                    };

                    commentsList.Add(commentJson);

                    if (comment.Replies is { } replies)
                    {
                        // Hoist replies as top-level comments
                        foreach (var replyEdge in replies.Edges)
                        {
                            var reply = replyEdge.Node;
                            var replyJson = new GithubCommentModel
                            {
                                Id = reply.Id,
                                ParentId = comment.Id,
                                Author = reply.Author?.Login ?? "??",
                                Content = reply.Body,
                                CreatedAt = reply.CreatedAt,
                                Url = reply.Url
                            };
                            commentsList.Add(replyJson);
                        }
                    }
                }

                hasMorePages = discussion.Comments?.PageInfo?.HasNextPage ?? false;
                endCursor = discussion.Comments?.PageInfo?.EndCursor;

                if (!hasMorePages)
                {
                    break;
                }

                retryCount = 0;
            }

            if (retryCount == _maxRetries)
            {
                throw new Exception("Max retries exceeded");
            }
        }

        return commentsList;
    }

    /// <summary>
    /// Gets recent issues from the last week for GitHub issues reporting
    /// </summary>
    /// <param name="repoOwner">Repository owner</param>
    /// <param name="repoName">Repository name</param>
    /// <param name="daysBack">Number of days to look back (default: 7)</param>
    /// <returns>List of GitHub issues from the specified time period</returns>
    public async Task<List<GithubIssueSummary>> GetRecentIssuesForReportAsync(string repoOwner, string repoName, int daysBack = 7)
    {
        var issuesList = new List<GithubIssueSummary>();
        var hasMorePages = true;
        string? endCursor = null;
        var retryCount = 0;
        var cutoffDate = DateTime.UtcNow.AddDays(-daysBack).ToString("yyyy-MM-dd");
        var searchQuery = $"repo:{repoOwner}/{repoName} is:issue created:>{cutoffDate}";

        while (hasMorePages)
        {
            var issuesQuery = @"
            query($query: String!, $first: Int!, $after: String) {
                search(query: $query, type: ISSUE, first: $first, after: $after) {
                    edges {
                        node {
                            ... on Issue {
                                id
                                number
                                author {
                                    login
                                }
                                title
                                body
                                url
                                createdAt
                                state
                                reactions {
                                    totalCount
                                }
                                labels(first: 10) {
                                    nodes {
                                        name
                                    }
                                }
                                comments {
                                    totalCount
                                }
                            }
                        }
                    }
                    pageInfo {
                        hasNextPage
                        endCursor
                    }
                }
            }";

            while (retryCount < _maxRetries)
            {
                var response = await _client.PostAsJsonAsync("https://api.github.com/graphql", new
                {
                    query = issuesQuery,
                    variables = new { query = searchQuery, first = 100, after = endCursor }
                });

                if (!response.IsSuccessStatusCode)
                {
                    await HandleRateLimit(response);
                    retryCount++;
                    continue;
                }

                var result = await response.Content.ReadAsStringAsync();
                var jsonDoc = System.Text.Json.JsonDocument.Parse(result);
                
                if (!jsonDoc.RootElement.TryGetProperty("data", out var dataElement) ||
                    !dataElement.TryGetProperty("search", out var searchElement))
                {
                    break;
                }

                var edgesElement = searchElement.GetProperty("edges");
                foreach (var edge in edgesElement.EnumerateArray())
                {
                    try
                    {
                        var node = edge.GetProperty("node");
                        
                        var labels = new List<string>();
                        if (node.TryGetProperty("labels", out var labelsElement))
                        {
                            var labelsNodes = labelsElement.GetProperty("nodes");
                            foreach (var labelNode in labelsNodes.EnumerateArray())
                            {
                                labels.Add(labelNode.GetProperty("name").GetString() ?? "");
                            }
                        }

                        var issueSummary = new GithubIssueSummary
                        {
                            Id = node.GetProperty("id").GetString() ?? "",
                            Title = node.GetProperty("title").GetString() ?? "",
                            CommentsCount = node.TryGetProperty("comments", out var commentsElement) && commentsElement.TryGetProperty("totalCount", out var commentsCountElement)
                                ? commentsCountElement.GetInt32()
                                : 0,
                            ReactionsCount = node.TryGetProperty("reactions", out var reactionsElement) && reactionsElement.TryGetProperty("totalCount", out var reactionsCountElement)
                                ? reactionsCountElement.GetInt32()
                                : 0,
                            Url = node.GetProperty("url").GetString() ?? "",
                            CreatedAt = DateTime.Parse(node.GetProperty("createdAt").GetString() ?? DateTime.UtcNow.ToString()),
                            State = node.GetProperty("state").GetString() ?? "",
                            Author = node.TryGetProperty("author", out var authorElement) && authorElement.ValueKind != JsonValueKind.Null && authorElement.TryGetProperty("login", out var loginElement) 
                                ? loginElement.GetString() ?? "unknown" 
                                : "unknown",
                            Labels = labels
                        };

                        issuesList.Add(issueSummary);
                    }
                    catch (Exception)
                    {
                        // Skip problematic issues and continue processing
                        continue;
                    }
                }

                var pageInfo = searchElement.GetProperty("pageInfo");
                hasMorePages = pageInfo.GetProperty("hasNextPage").GetBoolean();
                if (hasMorePages && pageInfo.TryGetProperty("endCursor", out var cursor))
                {
                    endCursor = cursor.GetString();
                }
                else
                {
                    hasMorePages = false;
                }

                retryCount = 0;
                break;
            }

            if (retryCount == _maxRetries)
            {
                throw new Exception("Max retries exceeded");
            }
        }

        return issuesList;
    }

    /// <summary>
    /// Gets the oldest important issues (open, high engagement) that have recent comment activity
    /// </summary>
    /// <param name="repoOwner">Repository owner</param>
    /// <param name="repoName">Repository name</param>
    /// <param name="recentDays">Number of days to consider "recent" for comment activity (default: 7)</param>
    /// <param name="topCount">Number of oldest issues to return (default: 3)</param>
    /// <returns>List of oldest important issues with recent comment activity</returns>
    public async Task<List<GithubIssueSummary>> GetOldestImportantIssuesWithRecentActivityAsync(string repoOwner, string repoName, int recentDays = 7, int topCount = 3)
    {
        var issuesList = new List<GithubIssueSummary>();
        var hasMorePages = true;
        string? endCursor = null;
        var retryCount = 0;
        var recentDate = DateTime.UtcNow.AddDays(-recentDays).ToString("yyyy-MM-dd");
        
        // Search for open issues that have been commented on recently, but are older than 30 days
        // This gives us old issues that are still getting attention
        var oldDate = DateTime.UtcNow.AddDays(-365).ToString("yyyy-MM-dd"); // Look back up to 1 year
        var searchQuery = $"repo:{repoOwner}/{repoName} is:issue is:open created:<{DateTime.UtcNow.AddDays(-30):yyyy-MM-dd} comments:>=2 updated:>{recentDate}";

        while (hasMorePages && issuesList.Count < topCount * 2) // Get extra issues to filter properly
        {
            var issuesQuery = @"
            query($query: String!, $first: Int!, $after: String) {
                search(query: $query, type: ISSUE, first: $first, after: $after) {
                    edges {
                        node {
                            ... on Issue {
                                id
                                number
                                author {
                                    login
                                }
                                title
                                body
                                url
                                createdAt
                                updatedAt
                                state
                                reactions {
                                    totalCount
                                }
                                labels(first: 10) {
                                    nodes {
                                        name
                                    }
                                }
                                comments {
                                    totalCount
                                }
                            }
                        }
                    }
                    pageInfo {
                        hasNextPage
                        endCursor
                    }
                }
            }";

            while (retryCount < _maxRetries)
            {
                var response = await _client.PostAsJsonAsync("https://api.github.com/graphql", new
                {
                    query = issuesQuery,
                    variables = new { query = searchQuery, first = 50, after = endCursor }
                });

                if (!response.IsSuccessStatusCode)
                {
                    await HandleRateLimit(response);
                    retryCount++;
                    continue;
                }

                var result = await response.Content.ReadAsStringAsync();
                var jsonDoc = System.Text.Json.JsonDocument.Parse(result);
                
                if (!jsonDoc.RootElement.TryGetProperty("data", out var dataElement) ||
                    !dataElement.TryGetProperty("search", out var searchElement))
                {
                    break;
                }

                var edgesElement = searchElement.GetProperty("edges");
                foreach (var edge in edgesElement.EnumerateArray())
                {
                    try
                    {
                        var node = edge.GetProperty("node");
                        
                        var labels = new List<string>();
                        if (node.TryGetProperty("labels", out var labelsElement))
                        {
                            var labelsNodes = labelsElement.GetProperty("nodes");
                            foreach (var labelNode in labelsNodes.EnumerateArray())
                            {
                                labels.Add(labelNode.GetProperty("name").GetString() ?? "");
                            }
                        }

                        var issueSummary = new GithubIssueSummary
                        {
                            Id = node.GetProperty("id").GetString() ?? "",
                            Title = node.GetProperty("title").GetString() ?? "",
                            CommentsCount = node.TryGetProperty("comments", out var commentsElement) && commentsElement.TryGetProperty("totalCount", out var commentsCountElement)
                                ? commentsCountElement.GetInt32()
                                : 0,
                            ReactionsCount = node.TryGetProperty("reactions", out var reactionsElement) && reactionsElement.TryGetProperty("totalCount", out var reactionsCountElement)
                                ? reactionsCountElement.GetInt32()
                                : 0,
                            Url = node.GetProperty("url").GetString() ?? "",
                            CreatedAt = DateTime.Parse(node.GetProperty("createdAt").GetString() ?? DateTime.UtcNow.ToString()),
                            State = node.GetProperty("state").GetString() ?? "",
                            Author = node.TryGetProperty("author", out var authorElement) && authorElement.ValueKind != JsonValueKind.Null && authorElement.TryGetProperty("login", out var loginElement) 
                                ? loginElement.GetString() ?? "unknown" 
                                : "unknown",
                            Labels = labels
                        };

                        issuesList.Add(issueSummary);
                    }
                    catch (Exception)
                    {
                        // Skip problematic issues and continue processing
                        continue;
                    }
                }

                var pageInfo = searchElement.GetProperty("pageInfo");
                hasMorePages = pageInfo.GetProperty("hasNextPage").GetBoolean();
                if (hasMorePages && pageInfo.TryGetProperty("endCursor", out var cursor))
                {
                    endCursor = cursor.GetString();
                }
                else
                {
                    hasMorePages = false;
                }

                retryCount = 0;
                break;
            }

            if (retryCount == _maxRetries)
            {
                throw new Exception("Max retries exceeded");
            }
        }

        // Filter and sort by oldest first, with minimum engagement threshold
        var minEngagement = 3; // Higher threshold since these are specifically important issues
        return issuesList
            .Where(i => i.State.Equals("OPEN", StringComparison.OrdinalIgnoreCase))
            .Where(i => (i.CommentsCount + i.ReactionsCount) >= minEngagement)
            .OrderBy(i => i.CreatedAt) // Oldest first
            .Take(topCount)
            .ToList();
    }

    private static async Task HandleRateLimit(HttpResponseMessage response)
    {
        if (response.Headers.RetryAfter is { } retryAfter)
        {
            var delay = retryAfter.Delta ?? TimeSpan.FromSeconds(60);
            await Task.Delay(delay);
        }
        else
        {
            await Task.Delay(TimeSpan.FromSeconds(60));
        }
    }
}