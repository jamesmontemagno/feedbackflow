using System.Net.Http.Headers;
using System.Net.Http.Json;
using SharedDump.Json;

namespace SharedDump.Models.GitHub;

public class GitHubService
{
    private readonly HttpClient _client;
    private readonly string _accessToken;
    private readonly int _maxRetries = 5;

    public GitHubService(string accessToken, HttpClient? client = null)
    {
        _accessToken = accessToken;
        _client = client ?? new HttpClient();
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