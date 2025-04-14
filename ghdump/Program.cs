using System.CommandLine;
using System.Diagnostics;
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

        var githubService = new GitHubService(accessToken!);

        if (!await githubService.CheckRepositoryValid(repoOwner, repoName))
        {
            Console.Error.WriteLine("Invalid repository");
            return 1;
        }

        var sw = Stopwatch.StartNew();

        var discussionsTask = discussionsIncluded ? githubService.GetDiscussionsAsync(repoOwner, repoName) : Task.FromResult(new List<GithubDiscussionModel>());
        var issuesTask = issuesIncluded ? githubService.GetIssuesAsync(repoOwner, repoName, labels) : Task.FromResult(new List<GithubIssueModel>());
        var pullsTask = pullsIncluded ? githubService.GetPullRequestsAsync(repoOwner, repoName, labels) : Task.FromResult(new List<GithubIssueModel>());

        await Task.WhenAll(discussionsTask, issuesTask, pullsTask);

        var discussions = await discussionsTask;
        var issues = await issuesTask;
        var pulls = await pullsTask;

        Console.WriteLine();
        Console.WriteLine($"Processing completed in {sw.Elapsed}.");

        if (issues.Count > 0)
        {
            var labelsPart = labels.Length > 0 ? String.Join("_", labels) : "all";
            var fileName = $"issues_{repoOwner}_{repoName}_{labelsPart}_output.json";

            var outputPath = Path.Combine(outputDirectory, fileName);
            await using var fileStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true);

            await JsonSerializer.SerializeAsync(fileStream, issues, GithubJsonContext.Default.ListGithubIssueModel);

            Console.WriteLine($"Processed {issues.Count} issues.");
            Console.WriteLine($"Issues have been written to {fileName}.");
        }
        else if (issuesIncluded)
        {
            Console.WriteLine("No issues found.");
        }

        Console.WriteLine();

        if (pulls.Count > 0)
        {
            var fileName = $"pullrequests_{repoOwner}_{repoName}_output.json";

            var outputPath = Path.Combine(outputDirectory, fileName);

            await using var fileStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true);
            await JsonSerializer.SerializeAsync(fileStream, pulls, GithubJsonContext.Default.ListGithubIssueModel);

            Console.WriteLine($"Processed {pulls.Count} pull-requests.");
            Console.WriteLine($"Pull-requests have been written to {fileName}.");
        }
        else if (pullsIncluded)
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
        else if (discussionsIncluded)
        {
            Console.WriteLine("No discussions found.");
        }

        return 0;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"An error occurred: {ex}");
        return 1;
    }
}
