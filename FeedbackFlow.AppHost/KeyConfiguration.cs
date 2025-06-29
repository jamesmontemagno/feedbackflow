#pragma warning disable ASPIREINTERACTION001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FeedbackFlow.AppHost;

public static class KeyConfiguration
{
    public static Keys Keys { get; } = new();

    public static async Task PromptForValues(string resourceName, IServiceProvider provider, string envDirectory,  bool restart, CancellationToken cancellationToken)
    {
        var interactionService = provider.GetRequiredService<IInteractionService>();
        var rcs = provider.GetRequiredService<ResourceCommandService>();
        var logger =  provider.GetRequiredService<ResourceLoggerService>().GetLogger(resourceName);

        var inputs = new InteractionInput[]
        {
            new() { InputType = InputType.SecretText, Label = "Azure Open AI Key", Placeholder = "Enter Azure Open AI Key", Value = Keys.AzureOpenAIKey },
            new() { InputType = InputType.Text, Label = "Azure Open AI Endpoint", Placeholder = "Enter Azure Open AI Endpoint", Value = Keys.AzureOpenAIEndpoint },
            new() { InputType = InputType.Choice, Label = "Azure Open AI Model", Placeholder = "Select an AI model",
            Options = [
                KeyValuePair.Create("gpt-4.1", "GPT 4.1"),
                KeyValuePair.Create("gpt-4.1-mini", "GPT 4.1 Mini"),
                KeyValuePair.Create("gpt-4.1-nano", "GPT 4.1 Nano"),
                KeyValuePair.Create("other", "Other"),
            ], Value = Keys.AzureOpenAIModel },
            new() { InputType = InputType.Text, Label = "Azure Open AI Other Model", Placeholder = "Enter Azure Open AI Model", Value = Keys.AzureOpenAIModel },
            new() { InputType = InputType.Text, Label = "BlueSky Username", Placeholder = "Enter BlueSky Username", Value = Keys.BlueSkyUsername },
            new() { InputType = InputType.SecretText, Label = "BlueSky App Password", Placeholder = "Enter BlueSky App Password", Value = Keys.BlueSkyAppPassword },
            new() { InputType = InputType.SecretText, Label = "YouTube API Key", Placeholder = "Enter YouTube API Key", Value = Keys.YouTubeApiKey },
            new() { InputType = InputType.SecretText, Label = "Reddit Client ID", Placeholder = "Enter Reddit Client ID", Value = Keys.RedditClientId },
            new() { InputType = InputType.SecretText, Label = "Reddit Client Secret", Placeholder = "Enter Reddit Client Secret", Value = Keys.RedditClientSecret },
            new() { InputType = InputType.SecretText, Label = "Twitter Bearer Token", Placeholder = "Enter Twitter Bearer Token", Value = Keys.TwitterBearerToken },
            new() { InputType = InputType.SecretText, Label = "GitHub Personal Access Token", Placeholder = "Enter GitHub Personal Access Token", Value = Keys.GitHubAccessToken },
            new() { InputType = InputType.SecretText, Label = "Mastodon Access Token", Placeholder = "Enter Mastodon Access Token", Value = Keys.MastodonAccessToken },
            new() { InputType = InputType.SecretText, Label = "Mastodon Client Key", Placeholder = "Enter Mastodon Client Key", Value = Keys.MastodonClientKey },
            new() { InputType = InputType.SecretText, Label = "Mastodon Client Secret", Placeholder = "Enter Mastodon Client Secret", Value = Keys.MastodonClientSecret },
            new() { InputType = InputType.Boolean, Label = "Use Mocks For All", Placeholder = "Use mock data for services", Value = Keys.UseMocks  ? "true" : "false" }
        };

        var result = await interactionService.PromptInputsAsync("API Keys", "Enter the API keys for services you would like to use. You can leave them blank and when debugging specific services will use mock data. Make sure to turn off Use Mocks For All when you want to use real data.", inputs, cancellationToken: cancellationToken);

        if (!result.Canceled)
        {
            // Get the values
            Keys.AzureOpenAIKey = result.Data[0].Value;
            Keys.AzureOpenAIEndpoint = result.Data[1].Value;
            Keys.AzureOpenAIModel = result.Data[2].Value == "other" ? result.Data[3].Value : result.Data[2].Value;
            Keys.BlueSkyUsername = result.Data[4].Value;
            Keys.BlueSkyAppPassword = result.Data[5].Value;
            Keys.YouTubeApiKey = result.Data[6].Value;
            Keys.RedditClientId = result.Data[7].Value;
            Keys.RedditClientSecret = result.Data[8].Value;
            Keys.TwitterBearerToken = result.Data[9].Value;
            Keys.GitHubAccessToken = result.Data[10].Value;
            Keys.MastodonAccessToken = result.Data[11].Value;
            Keys.MastodonClientKey = result.Data[12].Value;
            Keys.MastodonClientSecret = result.Data[13].Value;
            Keys.UseMocks = result.Data[14].Value == "true";

            logger.LogInformation("Set Endpoint = {Endpoint}", Keys.AzureOpenAIEndpoint);
            logger.LogInformation("Set Model = {Model}", Keys.AzureOpenAIModel);

            WriteToEnvFile(envDirectory);

            if (restart)
            {
                // Restart the resource after setting these values
                await rcs.ExecuteCommandAsync(resourceName, "resource-restart", cancellationToken);
            }
        }
        else
        {
            // Clear the values
            Keys.AzureOpenAIKey = null;
            Keys.AzureOpenAIEndpoint = null;
            Keys.AzureOpenAIModel = null;
            Keys.BlueSkyUsername = null;
            Keys.BlueSkyAppPassword = null;
            Keys.YouTubeApiKey = null;
            Keys.RedditClientId = null;
            Keys.RedditClientSecret = null;
            Keys.TwitterBearerToken = null;
            Keys.GitHubAccessToken = null;
            Keys.MastodonAccessToken = null;
            Keys.MastodonClientKey = null;
            Keys.MastodonClientSecret = null;
            Keys.UseMocks = true;
        }
    }

    public static bool HasEnvFile(string directory)
    {
        // Check if the .env file exists in the specified directory
        var envFilePath = Path.Combine(directory, ".env");
        return File.Exists(envFilePath);
    }

    public static void ReadFromConfiguration(EnvironmentCallbackContext context)
    {
        // Read the keys from the environment variables
        Keys.AzureOpenAIKey = context.EnvironmentVariables["Azure__OpenAI__ApiKey"] as string;
        Keys.AzureOpenAIEndpoint = context.EnvironmentVariables["Azure__OpenAI__Endpoint"] as string;
        Keys.AzureOpenAIModel = context.EnvironmentVariables["Azure__OpenAI__Deployment"] as string;
        Keys.BlueSkyUsername = context.EnvironmentVariables["BlueSky__Username"] as string;
        Keys.BlueSkyAppPassword = context.EnvironmentVariables["BlueSky__AppPassword"] as string;
        Keys.YouTubeApiKey = context.EnvironmentVariables["YouTube__ApiKey"] as string;
        Keys.RedditClientId = context.EnvironmentVariables["Reddit__ClientId"] as string;
        Keys.RedditClientSecret = context.EnvironmentVariables["Reddit__ClientSecret"] as string;
        Keys.TwitterBearerToken = context.EnvironmentVariables["Twitter__BearerToken"] as string;
        Keys.GitHubAccessToken = context.EnvironmentVariables["GitHub__AccessToken"] as string;
        Keys.MastodonAccessToken = context.EnvironmentVariables["Mastodon__AccessToken"] as string;
        Keys.MastodonClientKey = context.EnvironmentVariables["Mastodon__ClientKey"] as string;
        Keys.MastodonClientSecret = context.EnvironmentVariables["Mastodon__ClientSecret"] as string;
        Keys.UseMocks = context.EnvironmentVariables.ContainsKey("UseMocks") && bool.TryParse(context.EnvironmentVariables["UseMocks"] as string, out var useMocks) ? useMocks : true;
    }

    public static void WriteToConfiguration(EnvironmentCallbackContext context)
    {
        // Write the keys to the environment variables
        context.EnvironmentVariables["Azure__OpenAI__ApiKey"] = Keys.AzureOpenAIKey ?? string.Empty;
        context.EnvironmentVariables["Azure__OpenAI__Endpoint"] = Keys.AzureOpenAIEndpoint ?? string.Empty;
        context.EnvironmentVariables["Azure__OpenAI__Deployment"] = Keys.AzureOpenAIModel ?? string.Empty;
        context.EnvironmentVariables["BlueSky__Username"] = Keys.BlueSkyUsername ?? string.Empty;
        context.EnvironmentVariables["BlueSky__AppPassword"] = Keys.BlueSkyAppPassword ?? string.Empty;
        context.EnvironmentVariables["YouTube__ApiKey"] = Keys.YouTubeApiKey ?? string.Empty;
        context.EnvironmentVariables["Reddit__ClientId"] = Keys.RedditClientId ?? string.Empty;
        context.EnvironmentVariables["Reddit__ClientSecret"] = Keys.RedditClientSecret ?? string.Empty;
        context.EnvironmentVariables["Twitter__BearerToken"] = Keys.TwitterBearerToken ?? string.Empty;
        context.EnvironmentVariables["GitHub__AccessToken"] = Keys.GitHubAccessToken ?? string.Empty;
        context.EnvironmentVariables["Mastodon__AccessToken"] = Keys.MastodonAccessToken ?? string.Empty;
        context.EnvironmentVariables["Mastodon__ClientKey"] = Keys.MastodonClientKey ?? string.Empty;
        context.EnvironmentVariables["Mastodon__ClientSecret"] = Keys.MastodonClientSecret ?? string.Empty;
        context.EnvironmentVariables["UseMocks"] = Keys.UseMocks.ToString();
    }
    public static void ReadFromEnvFile(string directory)
    {
        // Read the values from the .env file if they exist and don't prompt the user
        var envFilePath = Path.Combine(directory, ".env");

        if (File.Exists(envFilePath))
        {
            var envLines = File.ReadAllLines(envFilePath);
            foreach (var line in envLines)
            {
                if (line.StartsWith("Azure__OpenAI__ApiKey="))
                {
                    Keys.AzureOpenAIKey = line.Split('=')[1].Trim();
                }
                else if (line.StartsWith("Azure__OpenAI__Endpoint="))
                {
                    Keys.AzureOpenAIEndpoint = line.Split('=')[1].Trim();
                }
                else if (line.StartsWith("Azure__OpenAI__Deployment="))
                {
                    Keys.AzureOpenAIModel = line.Split('=')[1].Trim();
                }
                else if (line.StartsWith("BlueSky__Username="))
                {
                    Keys.BlueSkyUsername = line.Split('=')[1].Trim();
                }
                else if (line.StartsWith("BlueSky__AppPassword="))
                {
                    Keys.BlueSkyAppPassword = line.Split('=')[1].Trim();
                }
                else if (line.StartsWith("YouTube__ApiKey="))
                {
                    Keys.YouTubeApiKey = line.Split('=')[1].Trim();
                }
                else if (line.StartsWith("Reddit__ClientId="))
                {
                    Keys.RedditClientId = line.Split('=')[1].Trim();
                }
                else if (line.StartsWith("Reddit__ClientSecret="))
                {
                    Keys.RedditClientSecret = line.Split('=')[1].Trim();
                }
                else if (line.StartsWith("Twitter__BearerToken="))
                {
                    Keys.TwitterBearerToken = line.Split('=')[1].Trim();
                }
                else if (line.StartsWith("GitHub__AccessToken="))
                {
                    Keys.GitHubAccessToken = line.Split('=')[1].Trim();
                }
                else if (line.StartsWith("Mastodon__AccessToken="))
                {
                    Keys.MastodonAccessToken = line.Split('=')[1].Trim();
                }
                else if (line.StartsWith("Mastodon__ClientKey="))
                {
                    Keys.MastodonClientKey = line.Split('=')[1].Trim();
                }
                else if (line.StartsWith("Mastodon__ClientSecret="))
                {
                    Keys.MastodonClientSecret = line.Split('=')[1].Trim();
                }
                else if (line.StartsWith("UseMocks="))
                {
                    if (bool.TryParse(line.Split('=')[1].Trim(), out var useMocks))
                    {
                        Keys.UseMocks = useMocks;
                    }
                }
            }
        }
    }
    public static void WriteToEnvFile(string directory)
    {
        var envFilePath = Path.Combine(directory, ".env");
        using var writer = new StreamWriter(envFilePath, false);
        if (!string.IsNullOrEmpty(Keys.AzureOpenAIKey))
            writer.WriteLine($"Azure__OpenAI__ApiKey={Keys.AzureOpenAIKey}");
        if (!string.IsNullOrEmpty(Keys.AzureOpenAIEndpoint))
            writer.WriteLine($"Azure__OpenAI__Endpoint={Keys.AzureOpenAIEndpoint}");
        if (!string.IsNullOrEmpty(Keys.AzureOpenAIModel))
            writer.WriteLine($"Azure__OpenAI__Deployment={Keys.AzureOpenAIModel}");
        if (!string.IsNullOrEmpty(Keys.BlueSkyUsername))
            writer.WriteLine($"BlueSky__Username={Keys.BlueSkyUsername}");
        if (!string.IsNullOrEmpty(Keys.BlueSkyAppPassword))
            writer.WriteLine($"BlueSky__AppPassword={Keys.BlueSkyAppPassword}");
        if (!string.IsNullOrEmpty(Keys.YouTubeApiKey))
            writer.WriteLine($"YouTube__ApiKey={Keys.YouTubeApiKey}");
        if (!string.IsNullOrEmpty(Keys.RedditClientId))
            writer.WriteLine($"Reddit__ClientId={Keys.RedditClientId}");
        if (!string.IsNullOrEmpty(Keys.RedditClientSecret))
            writer.WriteLine($"Reddit__ClientSecret={Keys.RedditClientSecret}");
        if (!string.IsNullOrEmpty(Keys.TwitterBearerToken))
            writer.WriteLine($"Twitter__BearerToken={Keys.TwitterBearerToken}");
        if (!string.IsNullOrEmpty(Keys.GitHubAccessToken))
            writer.WriteLine($"GitHub__AccessToken={Keys.GitHubAccessToken}");
        if (!string.IsNullOrEmpty(Keys.MastodonAccessToken))
            writer.WriteLine($"Mastodon__AccessToken={Keys.MastodonAccessToken}");
        if (!string.IsNullOrEmpty(Keys.MastodonClientKey))
            writer.WriteLine($"Mastodon__ClientKey={Keys.MastodonClientKey}");
        if (!string.IsNullOrEmpty(Keys.MastodonClientSecret))
            writer.WriteLine($"Mastodon__ClientSecret={Keys.MastodonClientSecret}");
        writer.WriteLine($"UseMocks={Keys.UseMocks}");
    }
}

public class Keys{
    public string? AzureOpenAIKey { get; set; }
    public string? AzureOpenAIEndpoint { get; set; }
    public string? AzureOpenAIModel { get; set; }
    public string? BlueSkyUsername { get; set; }
    public string? BlueSkyAppPassword { get; set; }
    public string? YouTubeApiKey { get; set; }
    public string? RedditClientId { get; set; }
    public string? RedditClientSecret { get; set; }
    public string? TwitterBearerToken { get; set; } 
    public string? GitHubAccessToken { get; set; }
    public string? MastodonAccessToken { get; set; }
    public string? MastodonClientKey { get; set; }
    public string? MastodonClientSecret { get; set; }
    public bool UseMocks { get; set; } = true;
}