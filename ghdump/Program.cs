using System.CommandLine;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using SharedDump.Models.GitHub;
using SharedDump.Json;

var repo = new Option<string?>(
    ["--repo", "-r"],
    "The repository to dump issues from. For example: dotnet/runtime");

var labels = new Option<string[]>(
    ["--label", "-l"],
    () => Array.Empty<string>(),
    "The label to filter issues by. Can be specified multiple times.");

var includeIssues = new Option<bool?>(
    ["--issues"],
    "Include issues in the output.");

var includePullRequests = new Option<bool?>(
    ["--pulls"],
    "Include pull requests in the output.");

var includeDiscussions = new Option<bool?>(
    ["--discussions"],
    "Include discussions in the output.");

var accessToken = new Option<string?>(
    ["--token", "-t"],
    "The GitHub access token to use. If not specified, will look in environment variables or user secrets.");

var outputDirectory = new Option<string?>(
    ["--output", "-o"],
    () => null,
    "The directory where the results will be written. Defaults to the current directory.");

var rootCommand = new RootCommand("CLI tool for extracting GitHub issues and discussions in JSON format.")
{
    repo,
    labels,
    includeIssues,
    includePullRequests,
    includeDiscussions,
    accessToken,
    outputDirectory
};

rootCommand.SetHandler(RunAsync, repo, labels, includeIssues, includePullRequests, includeDiscussions, accessToken, outputDirectory);

await rootCommand.InvokeAsync(args);

async Task<int> RunAsync(string? repo, string[] labels, bool? includeIssues, bool? includePullRequests, bool? includeDiscussions, string? accessToken, string? outputDirectory)
{
    var config = new ConfigurationBuilder()
        .AddEnvironmentVariables()
        .AddUserSecrets<Program>()
        .AddJsonFile("appsettings.json", optional: true)
        .Build();

    // Check if we have a token in user secrets
    var configAccessToken = config["Github:AccessToken"];

    try
    {
        accessToken ??= configAccessToken;
        outputDirectory ??= Environment.CurrentDirectory;

        bool issuesIncluded = includeIssues ?? false;
        bool pullsIncluded = includePullRequests ?? false;
        bool discussionsIncluded = includeDiscussions ?? false;

        if (!issuesIncluded && !pullsIncluded && !discussionsIncluded)
        {
            // If no options are specified, include issues by default
            issuesIncluded = true;
        }

        var (repoOwner, repoName) = repo?.Trim().Split('/', StringSplitOptions.RemoveEmptyEntries) switch
        {
            [var owner, var name] => (owner, name),
            _ => throw new InvalidOperationException("Invalid repository format, expected owner/repo. Example: dotnet/aspnetcore.")
        };

        var maxRetries = 5;
        var retryCount = 0;

        using var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Authorization = new("Bearer", accessToken);
        var an = typeof(Program).Assembly.GetName();
        httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(an.Name!, $"{an.Version}"));

        // Print out the repository information
        Console.WriteLine($"Repository: {repoOwner}/{repoName}");

        if (labels.Length > 0)
        {
            Console.WriteLine($"Labels: {string.Join(", ", labels)}");
        }
        else
        {
            Console.WriteLine("No Labels specified.");
        }

        Console.WriteLine($"Including issues: {(issuesIncluded ? "yes" : "no")}");
        Console.WriteLine($"Including pull-requests: {(pullsIncluded ? "yes" : "no")}");
        Console.WriteLine($"Including discussions: {(discussionsIncluded ? "yes" : "no")}");
        Console.WriteLine($"Results directory: {outputDirectory}");

        if (!await CheckRepositoryValid(repoOwner, repoName))
        {
            Console.Error.WriteLine("Invalid repository");
            return 1;
        }

        var sw = Stopwatch.StartNew();

        var discussionsTask = GetDiscussionsAsync(discussionsIncluded);
        var issuesTask = GetIssuesAsync(labels, issuesIncluded);
        var pullsTask = GetPullRequestsAsync(labels, pullsIncluded);

        await Task.WhenAll(discussionsTask, issuesTask, pullsTask);

        var discussions = await discussionsTask;
        var issues = await issuesTask;
        var pulls = await pullsTask;

        await WriteResultsToDisk(outputDirectory, issuesIncluded, issues, pullsIncluded, pulls, discussionsIncluded, discussions, sw.Elapsed);

        return 0;

        async Task<bool> CheckRepositoryValid(string repoOwner, string repoName)
        {
            // Check if this repository is valid
            var checkRepoQuery = @"
            query($owner: String!, $name: String!) {
                repository(owner: $owner, name: $name) {
                    id
                }
            }";

            var response = await httpClient.PostAsJsonAsync("https://api.github.com/graphql", new GithubRepositoryQuery
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

        async Task WriteResultsToDisk(
            string outputDirectory,
            bool includeIssues,
            List<GithubIssueModel> issues,
            bool includePullRequests,
            List<GithubIssueModel> pullRequests,
            bool includeDiscussions,
            List<GithubDiscussionModel> discussions,
            TimeSpan elapsed)
        {
            Console.WriteLine();
            Console.WriteLine($"Processing completed in {elapsed}.");

            if (issues.Count > 0)
            {
                var labelsPart = labels.Length > 0 ? string.Join("_", labels) : "all";
                var fileName = $"issues_{repoOwner}_{repoName}_{labelsPart}_output.json";

                var outputPath = Path.Combine(outputDirectory, fileName);
                await using var fileStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true);

                await JsonSerializer.SerializeAsync(fileStream, issues, GithubJsonContext.Default.ListGithubIssueModel);

                Console.WriteLine($"Processed {issues.Count} issues.");
                Console.WriteLine($"Issues have been written to {fileName}.");
            }
            else if (includeIssues)
            {
                Console.WriteLine("No issues found.");
            }

            Console.WriteLine();

            if (pullRequests.Count > 0)
            {
                var fileName = $"pullrequests_{repoOwner}_{repoName}_output.json";

                var outputPath = Path.Combine(outputDirectory, fileName);

                await using var fileStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true);
                await JsonSerializer.SerializeAsync(fileStream, pullRequests, GithubJsonContext.Default.ListGithubIssueModel);

                Console.WriteLine($"Processed {pullRequests.Count} pull-requests.");
                Console.WriteLine($"Pull-requests have been written to {fileName}.");
            }
            else if (includePullRequests)
            {
                Console.WriteLine("No pull-requests found.");
            }

            Console.WriteLine();

            if (discussions.Count > 0)
            {
                var fileName = $"discussions_{repoOwner}_{repoName}_output.json";

                var outputPath = Path.Combine(outputDirectory, fileName);
                await using var fileStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true);

                await JsonSerializer.SerializeAsync(fileStream, discussions, GithubJsonContext.Default.ListGithubDiscussionModel);

                Console.WriteLine($"Processed {discussions.Count} discussions.");
                Console.WriteLine($"Discussions have been written to {fileName}.");
            }
            else if (includeDiscussions)
            {
                Console.WriteLine("No discussions found.");
            }
        }

        async Task<List<GithubDiscussionModel>> GetDiscussionsAsync(bool includeDiscussions)
        {
            if (!includeDiscussions)
            {
                return new List<GithubDiscussionModel>();
            }

            var discussionsList = new List<GithubDiscussionModel>();

            var hasMorePages = true;
            string? endCursor = null;

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

                // Retry logic for handling rate limits
                while (retryCount < maxRetries)
                {
                    var response = await httpClient.PostAsJsonAsync("https://api.github.com/graphql", new GithubDiscussionQuery
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

                        // TODO: Walk through all pages of comments (properly)
                        //string? commentsCursor = null;

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

                if (retryCount == maxRetries)
                {
                    throw new Exception("Max retries exceeded");
                }
            }

            return discussionsList;
        }

        async Task<List<GithubIssueModel>> GetIssuesAsync(string[] labels, bool includeIssues)
        {
            if (!includeIssues)
            {
                return new List<GithubIssueModel>();
            }

            var issuesList = new List<GithubIssueModel>();

            var hasMorePages = true;
            string? endCursor = null;

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

                // Retry logic for handling rate limits
                while (retryCount < maxRetries)
                {
                    var response = await httpClient.PostAsJsonAsync("https://api.github.com/graphql", new GithubIssueQuery
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

                        // TODO: Walk through all pages of comments (properly)
                        //var commentsCursor = "";

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

                if (retryCount == maxRetries)
                {
                    throw new Exception("Max retries exceeded");
                }
            }

            return issuesList;
        }

        async Task<List<GithubIssueModel>> GetPullRequestsAsync(string[] labels, bool includePullRequests)
        {
            if (!includePullRequests)
            {
                return new List<GithubIssueModel>();
            }

            var pullsList = new List<GithubIssueModel>();

            var hasMorePages = true;
            string? endCursor = null;

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

                // Retry logic for handling rate limits
                while (retryCount < maxRetries)
                {
                    var response = await httpClient.PostAsJsonAsync("https://api.github.com/graphql", new GithubIssueQuery
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

                        // TODO: Walk through all pages of comments (properly)
                        //var commentsCursor = "";

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

                if (retryCount == maxRetries)
                {
                    throw new Exception("Max retries exceeded");
                }
            }

            return pullsList;
        }

        async Task HandleRateLimit(HttpResponseMessage response)
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
    catch (Exception ex)
    {
        Console.Error.WriteLine($"An error occurred: {ex}");
        return 1;
    }
}
